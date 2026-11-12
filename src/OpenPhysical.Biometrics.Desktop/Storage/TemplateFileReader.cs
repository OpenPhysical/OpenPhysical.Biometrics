// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class TemplateFileReader
{
    internal static async Task<Result<byte[], DesktopError>> ReadAsync(
        string path,
        int maximumBytes,
        CancellationToken cancellationToken
    )
    {
        var result = await DesktopEdge
            .TryAsync(
                async () =>
                {
                    await using var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        4096,
                        FileOptions.Asynchronous
                    );
                    if (stream.Length > maximumBytes)
                        return Result.Failure<byte[], DesktopError>(
                            new DesktopError("Storage.Size", "The file exceeds the supported size.")
                        );
                    var bytes = new byte[(int)stream.Length];
                    await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
                    return Result.Success<byte[], DesktopError>(bytes);
                },
                "Storage.Read",
                "The template package file could not be read."
            )
            .ConfigureAwait(false);
        return result.Bind(value => value);
    }
}
