// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Runtime.InteropServices;

internal static class AvFoundationNative
{
    private const string Library = "openphysical_camera";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void FrameCallback(
        nint context,
        nint bgra,
        nuint length,
        int width,
        int height,
        nuint stride,
        long timestampNanoseconds,
        int status
    );

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int opbc_list(out nint json);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void opbc_free(nint buffer);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int opbc_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string id,
        int width,
        int height,
        int numerator,
        int denominator,
        out nint handle
    );

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int opbc_start(nint handle, FrameCallback callback, nint context);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int opbc_stop(nint handle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void opbc_release(nint handle);
}
