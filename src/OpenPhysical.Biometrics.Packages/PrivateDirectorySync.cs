// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

internal static class PrivateDirectorySync
{
    internal static UnitResult<PackageError> Flush(string path)
    {
        if (OperatingSystem.IsWindows())
            return UnitResult.Success<PackageError>();
        var directory = OperatingSystem.IsMacOS() ? 0x100000 : 0x10000;
        var noFollow = OperatingSystem.IsMacOS() ? 0x100 : 0x20000;
        var descriptor = Open(path, directory | noFollow);
        if (descriptor < 0)
            return Failure();
        try
        {
            return Fsync(descriptor) == 0 ? UnitResult.Success<PackageError>() : Failure();
        }
        finally
        {
            Close(descriptor);
        }
    }

    private static PackageError Failure() =>
        new(
            PackageErrorCodes.FileAccessFailed,
            "The private output directory could not be flushed safely."
        );

    [DllImport("libc", EntryPoint = "open", SetLastError = true, ExactSpelling = true)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true, ExactSpelling = true)]
    private static extern int Fsync(int descriptor);

    [DllImport("libc", EntryPoint = "close", SetLastError = true, ExactSpelling = true)]
    private static extern int Close(int descriptor);
}
