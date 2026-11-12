// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;

internal static class PrivatePackageFiles
{
    internal static async Task<string> WriteAsync(
        ImmutableArray<TemplatePackageFile> files,
        string folder,
        CancellationToken cancellationToken
    )
    {
        var suffix = $"capture-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var staging = Path.Combine(folder, "." + suffix);
        var destination = Path.Combine(folder, suffix);
        PrivateFileWriter.CreateDirectory(staging);
        var completed = false;
        try
        {
            foreach (var file in files)
                await PrivateFileWriter
                    .WriteAsync(Path.Combine(staging, file.Name), file.Bytes, cancellationToken)
                    .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
            completed = true;
            return Path.Combine(destination, "fingerprints.fmr");
        }
        finally
        {
            if (!completed && Directory.Exists(staging))
                Directory.Delete(staging, true);
        }
    }
}
