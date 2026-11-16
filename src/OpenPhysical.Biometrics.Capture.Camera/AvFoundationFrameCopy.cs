// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Imaging;

internal static class AvFoundationFrameCopy
{
    internal static Result<RgbImage, CameraError> Copy(
        nint pointer,
        nuint length,
        int width,
        int height,
        nuint stride,
        CameraMode mode
    )
    {
        if (
            pointer == nint.Zero
            || width != mode.Width
            || height != mode.Height
            || width < 1
            || height < 1
            || (long)width * height > 32 * 1024 * 1024
            || stride > int.MaxValue
            || stride < (nuint)((long)width * 4)
            || stride * (nuint)height > length
            || length > 128 * 1024 * 1024
        )
            return AvFoundationStatus.Error(4);
        var bytes = new byte[(int)stride * height];
        try
        {
            Marshal.Copy(pointer, bytes, 0, bytes.Length);
            return RgbImageBytes
                .FromBgraBytes(bytes, width, height, (int)stride)
                .MapError(_ => AvFoundationStatus.Error(4));
        }
        finally
        {
            Array.Clear(bytes);
        }
    }
}
