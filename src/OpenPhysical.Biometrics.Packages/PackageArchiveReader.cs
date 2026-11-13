// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;
using CSharpFunctionalExtensions;

internal static class PackageArchiveReader
{
    internal static Result<BiometricPackage, PackageError> Read(
        byte[] plaintext,
        PackageProtectionPolicy policy
    ) =>
        PackageEdge.Bind(
            () => Decode(plaintext, policy),
            PackageErrorCodes.InvalidArchive,
            "The authenticated package archive is malformed."
        );

    private static Result<BiometricPackage, PackageError> Decode(
        byte[] plaintext,
        PackageProtectionPolicy policy
    )
    {
        var resources = PackageZipResourceGuard.Validate(plaintext, policy);
        if (resources.IsFailure)
            return resources.Error;
        using var memory = new MemoryStream(plaintext, false);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        var valid = Validate(archive, policy);
        if (valid.IsFailure)
            return valid.Error;
        var read = ReadEntry(archive.Entries.Single(value => value.FullName == "manifest.json"));
        if (read.IsFailure)
            return read.Error;
        var manifestBytes = read.Value;
        try
        {
            var manifest = PackageManifest.Read(manifestBytes);
            if (manifest.IsFailure)
                return manifest.Error;
            return Materialize(archive, manifest.Value);
        }
        finally
        {
            Array.Clear(manifestBytes);
        }
    }

    private static Result<BiometricPackage, PackageError> Materialize(
        ZipArchive archive,
        PackageManifest manifest
    )
    {
        var artifacts = ReadArtifacts(archive, manifest);
        if (artifacts.IsFailure)
            return artifacts.Error;
        var evidence = Encoding.UTF8.GetBytes(manifest.Evidence.GetRawText());
        try
        {
            return BiometricPackage.Create(
                manifest.PersonId,
                manifest.Label,
                string.IsNullOrEmpty(manifest.ExternalId)
                    ? Maybe<string>.None
                    : manifest.ExternalId,
                evidence,
                artifacts.Value
            );
        }
        finally
        {
            Array.Clear(evidence);
        }
    }

    private static UnitResult<PackageError> Validate(
        ZipArchive archive,
        PackageProtectionPolicy policy
    )
    {
        var entries = archive.Entries;
        if (
            entries.Count is < 2
            || entries.Count > policy.MaximumEntries
            || entries
                .Select(value => value.FullName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != entries.Count
            || entries.Count(value => value.FullName == "manifest.json") != 1
            || entries.Any(value =>
                value.FullName != "manifest.json" && !PackageNames.Valid(value.FullName)
            )
            || entries.Any(value =>
                ((value.ExternalAttributes >> 16) & 0xF000) is not (0 or 0x8000)
                || (value.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0
            )
        )
            return new PackageError(
                PackageErrorCodes.InvalidArchive,
                "The archive has duplicate, missing, or unsafe member names."
            );
        if (
            entries.Any(value =>
                value.Length < 0
                || value.Length > policy.MaximumEntryBytes
                || value.FullName == "manifest.json" && value.Length > PackageJson.MaximumBytes
            )
            || entries.Sum(value => value.Length) > policy.MaximumPlaintextBytes
        )
            return new PackageError(
                PackageErrorCodes.LimitExceeded,
                "The archive exceeds its uncompressed resource limits."
            );
        return UnitResult.Success<PackageError>();
    }

    private static Result<ImmutableArray<PackageArtifact>, PackageError> ReadArtifacts(
        ZipArchive archive,
        PackageManifest manifest
    )
    {
        if (
            manifest.Artifacts.Length != archive.Entries.Count - 1
            || manifest.Artifacts.Any(value => !PackageNames.Valid(value.Name))
            || manifest
                .Artifacts.Select(value => value.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != manifest.Artifacts.Length
        )
            return Mismatch();
        var artifacts = ImmutableArray.CreateBuilder<PackageArtifact>();
        foreach (var item in manifest.Artifacts)
        {
            var entry = archive.Entries.FirstOrDefault(value => value.FullName == item.Name);
            if (entry is not ZipArchiveEntry member || member.Length != item.Length)
                return Mismatch();
            var read = ReadEntry(member);
            if (read.IsFailure)
                return read.Error;
            var bytes = read.Value;
            try
            {
                if (!string.Equals(PackageHash.Of(bytes), item.Sha256, StringComparison.Ordinal))
                    return Mismatch();
                var artifact = PackageArtifact.Create(item.Name, bytes, item.Role, item.Format);
                if (artifact.IsFailure)
                    return artifact.Error;
                artifacts.Add(artifact.Value);
            }
            finally
            {
                Array.Clear(bytes);
            }
        }
        return artifacts.ToImmutable();
    }

    private static Result<byte[], PackageError> ReadEntry(ZipArchiveEntry entry)
    {
        var bytes = new byte[(int)entry.Length];
        using var stream = entry.Open();
        var complete = false;
        try
        {
            stream.ReadExactly(bytes);
            complete = stream.ReadByte() == -1;
            return complete
                ? bytes
                : new PackageError(
                    PackageErrorCodes.InvalidArchive,
                    "An archive member exceeds its advertised length."
                );
        }
        finally
        {
            if (!complete)
                Array.Clear(bytes);
        }
    }

    private static PackageError Mismatch() =>
        new(
            PackageErrorCodes.ArtifactMismatch,
            "The archive members do not match their manifest names, lengths, or hashes."
        );
}
