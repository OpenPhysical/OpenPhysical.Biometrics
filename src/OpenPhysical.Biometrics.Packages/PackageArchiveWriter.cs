// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.IO.Compression;
using CSharpFunctionalExtensions;

internal static class PackageArchiveWriter
{
    internal static Result<byte[], PackageError> Write(
        BiometricPackage package,
        PackageProtectionPolicy policy
    ) =>
        PackageEdge.Bind(
            () => Encode(package, policy),
            PackageErrorCodes.InvalidArchive,
            "The package archive could not be encoded."
        );

    private static Result<byte[], PackageError> Encode(
        BiometricPackage package,
        PackageProtectionPolicy policy
    )
    {
        if (
            package.Artifacts.Length + 1 > policy.MaximumEntries
            || package.Artifacts.Any(value => value.Bytes.Length > policy.MaximumEntryBytes)
            || package.Artifacts.Sum(value => (long)value.Bytes.Length)
                > policy.MaximumPlaintextBytes
        )
            return Limit();
        var manifest = PackageManifest.Write(package);
        try
        {
            if (
                manifest.Length > PackageJson.MaximumBytes
                || manifest.Length > policy.MaximumEntryBytes
            )
                return Limit();
            using var memory = new SensitiveMemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                Write(archive, "manifest.json", manifest);
                foreach (var artifact in package.Artifacts)
                    Write(archive, artifact.Name, artifact.Bytes.AsSpan());
            }
            return memory.Length > policy.MaximumPlaintextBytes ? Limit() : memory.ToArray();
        }
        finally
        {
            Array.Clear(manifest);
        }
    }

    private static bool Write(ZipArchive archive, string name, ReadOnlySpan<byte> bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var stream = entry.Open();
        stream.Write(bytes);
        return true;
    }

    private static PackageError Limit() =>
        new(
            PackageErrorCodes.LimitExceeded,
            "The package exceeds its archive, entry, or member limit."
        );
}
