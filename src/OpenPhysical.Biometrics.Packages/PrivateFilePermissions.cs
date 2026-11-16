// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

internal static class PrivateFilePermissions
{
    internal static DirectoryInfo CreateDirectory(string path) =>
        OperatingSystem.IsWindows()
            ? WindowsPrivateFiles.CreateDirectory(path)
            : Directory.CreateDirectory(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );

    internal static async Task<bool> WriteAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken token
    )
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = OperatingSystem.IsWindows()
            ? WindowsPrivateFiles.CreateFile(path)
            : new FileStream(path, options);
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
        stream.Flush(true);
        return true;
    }
}
