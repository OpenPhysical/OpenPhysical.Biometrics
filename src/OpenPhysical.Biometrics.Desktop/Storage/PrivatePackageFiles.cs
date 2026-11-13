// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

internal static class PrivatePackageFiles
{
    internal static async Task<Result<string, DesktopError>> WriteAsync(
        ImmutableArray<TemplatePackageFile> files,
        string folder,
        CancellationToken token
    )
    {
        var destination = Path.Combine(
            folder,
            $"capture-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"
        );
        var writes = files
            .Select(file => new PrivateFileWrite(
                Path.Combine(destination, file.Name),
                [.. file.Bytes]
            ))
            .ToImmutableArray();
        var saved = await PrivateFileTransaction
            .WriteBatchAsync(writes, false, token)
            .ConfigureAwait(false);
        return saved.IsFailure
            ? new DesktopError(saved.Error.Code, saved.Error.Message)
            : Path.Combine(destination, "fingerprints.fmr");
    }
}
