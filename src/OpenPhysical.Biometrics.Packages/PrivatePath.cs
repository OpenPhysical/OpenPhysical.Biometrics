// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;

internal static class PrivatePath
{
    internal static Result<string, PackageError> Validate(string path) =>
        PackageEdge.Bind(
            () => Check(path),
            PackageErrorCodes.UnsafePath,
            "Choose a private destination outside repositories and linked paths."
        );

    private static Result<string, PackageError> Check(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Unsafe();
        var full = Path.GetFullPath(path);
        var current = full;
        while (current.Length > 0)
        {
            FileSystemInfo info = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            if (
                Maybe.From(info.LinkTarget).HasValue
                || (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            )
                return Unsafe();
            if (
                Directory.Exists(current)
                && (
                    Directory.Exists(Path.Combine(current, ".git"))
                    || File.Exists(Path.Combine(current, ".git"))
                )
            )
                return Unsafe();
            var parent = Maybe.From(Path.GetDirectoryName(current));
            if (parent.HasNoValue || parent.Value == current)
                break;
            current = parent.Value;
        }
        return full;
    }

    private static PackageError Unsafe() =>
        new(
            PackageErrorCodes.UnsafePath,
            "Biometric files cannot be written through links or inside a repository."
        );
}
