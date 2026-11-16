// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class PrivateFilePlan
{
    internal static Result<ImmutableArray<PrivateFileInstallation>, PackageError> Create(
        ImmutableArray<PrivateFileWrite> files,
        bool overwrite
    ) =>
        PackageEdge.Bind(
            () => Plan(files, overwrite),
            PackageErrorCodes.UnsafePath,
            "The private output plan is invalid."
        );

    private static Result<ImmutableArray<PrivateFileInstallation>, PackageError> Plan(
        ImmutableArray<PrivateFileWrite> files,
        bool overwrite
    )
    {
        if (files.IsDefaultOrEmpty || files.Length > 256)
            return new PackageError(
                PackageErrorCodes.UnsafePath,
                "Provide one to 256 distinct private output paths."
            );
        var entries = ImmutableArray.CreateBuilder<PrivateFileInstallation>();
        foreach (var file in files)
        {
            var entry = PlanFile(file, overwrite);
            if (entry.IsFailure)
                return entry.Error;
            entries.Add(entry.Value);
        }
        return
            entries
                .Select(value => value.File.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != entries.Count
            ? new PackageError(PackageErrorCodes.UnsafePath, "Output paths must be distinct.")
            : entries.ToImmutable();
    }

    private static Result<PrivateFileInstallation, PackageError> PlanFile(
        PrivateFileWrite file,
        bool overwrite
    )
    {
        var path = PrivatePath.Validate(file.Path);
        if (path.IsFailure)
            return path.Error;
        if (Directory.Exists(path.Value))
            return new PackageError(
                PackageErrorCodes.UnsafePath,
                "A file destination names a directory."
            );
        if (File.Exists(path.Value) && !overwrite)
            return new PackageError(
                PackageErrorCodes.DestinationExists,
                "An output already exists. Explicit replacement is required."
            );
        var parent = Maybe.From(Path.GetDirectoryName(path.Value)).GetValueOrDefault(string.Empty);
        var prefix = Path.Combine(parent, ".opbio-" + Guid.NewGuid().ToString("N"));
        return new PrivateFileInstallation(
            file with
            {
                Path = path.Value,
            },
            prefix + ".stage",
            prefix + ".backup"
        );
    }
}
