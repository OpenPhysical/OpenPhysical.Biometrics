// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Buffers.Binary;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Imaging;

/// <summary>Adapts known native camera layouts without guessing YUV conversion matrices.</summary>
public static class CameraBufferDecoder
{
    /// <summary>Decodes a provider-owned image packet into an independent RGB image.</summary>
    /// <param name="bytes">Complete FlashCap image packet.</param>
    /// <param name="mode">Actual advertised native mode.</param>
    /// <param name="avFoundation">Whether AVFoundation supplied top-down native RGB bytes in a DIB wrapper.</param>
    /// <returns>An owned image or a precise unsupported/invalid-frame failure.</returns>
    public static Result<RgbImage, CameraError> Decode(
        ReadOnlyMemory<byte> bytes,
        CameraMode mode,
        bool avFoundation
    ) =>
        !Supported(mode.PixelFormat) ? Invalid("The native camera color format is unsupported.")
        : avFoundation && mode.PixelFormat is "RGB24" or "RGB32" or "ARGB32"
            ? NativeRgb(bytes.Span, mode)
                .MapError(error =>
                    error with
                    {
                        NativeLayout = CameraNativeLayout.Describe(bytes.Span, mode),
                    }
                )
        : RgbImageDecoder
            .Load(bytes)
            .MapError(_ => Invalid("The camera image could not be decoded."))
            .Ensure(
                image => image.Width == mode.Width && image.Height == mode.Height,
                Invalid("Camera image geometry changed.")
            );

    internal static bool Supported(string format) =>
        format is "RGB24" or "RGB32" or "ARGB32" or "JPEG" or "PNG";

    private static Result<RgbImage, CameraError> NativeRgb(
        ReadOnlySpan<byte> bytes,
        CameraMode mode
    )
    {
        if (
            bytes.Length < 54
            || bytes[0] != 'B'
            || bytes[1] != 'M'
            || mode.Width is < 1 or > 16384
            || mode.Height is < 1 or > 16384
        )
            return Invalid("The native RGB packet header is invalid.");
        var offset = BinaryPrimitives.ReadInt32LittleEndian(bytes[10..]);
        var width = BinaryPrimitives.ReadInt32LittleEndian(bytes[18..]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(bytes[22..]);
        var channels = mode.PixelFormat == "RGB24" ? 3 : 4;
        if (
            BinaryPrimitives.ReadInt32LittleEndian(bytes[14..]) != 40
            || BinaryPrimitives.ReadInt16LittleEndian(bytes[26..]) != 1
            || BinaryPrimitives.ReadInt16LittleEndian(bytes[28..]) != channels * 8
        )
            return Invalid("The native RGB bit depth or header layout is invalid.");
        if (
            offset < 54
            || offset >= bytes.Length
            || width != mode.Width
            || height != mode.Height
            || (bytes.Length - offset) % height != 0
        )
            return Invalid("The native RGB packet does not match its camera mode.");
        var stride = (bytes.Length - offset) / height;
        if (stride < width * channels || stride > width * channels + 256)
            return Invalid("The native RGB row stride is unsupported.");
        return CopyRgb(bytes[offset..], mode, stride, channels);
    }

    private static Result<RgbImage, CameraError> CopyRgb(
        ReadOnlySpan<byte> bytes,
        CameraMode mode,
        int stride,
        int channels
    )
    {
        var rgb = new byte[mode.Width * mode.Height * 3];
        var (red, green, blue) = mode.PixelFormat switch
        {
            "ARGB32" => (2, 1, 0),
            "RGB32" => (1, 2, 3),
            _ => (0, 1, 2),
        };
        try
        {
            for (var y = 0; y < mode.Height; y++)
            for (var x = 0; x < mode.Width; x++)
            {
                var from = y * stride + x * channels;
                var to = (y * mode.Width + x) * 3;
                rgb[to] = bytes[from + red];
                rgb[to + 1] = bytes[from + green];
                rgb[to + 2] = bytes[from + blue];
            }
            return RgbImageBytes
                .FromRgbBytes(rgb, mode.Width, mode.Height)
                .MapError(_ => Invalid("The native RGB dimensions are unsupported."));
        }
        finally
        {
            Array.Clear(rgb);
        }
    }

    private static CameraError Invalid(string message) =>
        new(CameraErrorCodes.InvalidFrame, message);
}
