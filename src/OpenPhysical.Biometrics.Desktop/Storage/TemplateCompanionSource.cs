// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

internal sealed class TemplateCompanionSource(
    Func<string, int, CancellationToken, Task<Result<byte[], DesktopError>>> read
)
{
    internal Task<Result<byte[], DesktopError>> ReadAsync(
        string name,
        int maximum,
        CancellationToken token
    ) => read(name, maximum, token);

    internal static TemplateCompanionSource FromFolder(string folder) =>
        new((name, maximum, token) => ReadFileAsync(Path.Combine(folder, name), maximum, token));

    internal static TemplateCompanionSource FromFiles(ImmutableArray<PackageArtifact> files) =>
        new((name, maximum, token) => Task.FromResult(ReadMemory(files, name, maximum, token)));

    private static Result<byte[], DesktopError> ReadMemory(
        ImmutableArray<PackageArtifact> files,
        string name,
        int maximum,
        CancellationToken token
    )
    {
        if (token.IsCancellationRequested)
            return new DesktopError("Cancelled", "Package inspection was cancelled.");
        var matching = files.Where(file => file.Name == name).ToArray();
        return matching.Length == 1 && matching[0].Bytes.Length <= maximum
            ? matching[0].Bytes.ToArray()
            : new DesktopError(
                "Storage.Manifest",
                "A required companion is unavailable or exceeds its size limit."
            );
    }

    private static async Task<Result<byte[], DesktopError>> ReadFileAsync(
        string path,
        int maximum,
        CancellationToken token
    )
    {
        var attributes = DesktopEdge.Try(
            () => File.GetAttributes(path),
            "Storage.Manifest",
            "A required companion is unavailable."
        );
        if (attributes.IsFailure)
            return attributes.Error;
        if ((attributes.Value & FileAttributes.ReparsePoint) != 0)
            return new DesktopError("Storage.Manifest", "Companions must not be symbolic links.");
        return await TemplateFileReader.ReadAsync(path, maximum, token).ConfigureAwait(false);
    }
}
