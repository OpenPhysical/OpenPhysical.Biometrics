// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using SkiaSharp;

/// <summary>Encodes prepared sRGB pixels for portrait previews and PIV records.</summary>
public static class PortraitEncoding
{
    /// <summary>Encodes a complete image at a whole-image ratio no greater than 15:1.</summary>
    /// <param name="rgb">Tightly packed, opaque sRGB pixels.</param>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <returns>Owned JP2 bytes, or a failure for invalid pixels or an encoding that exceeds the compression limit.</returns>
    public static Result<byte[], FacialError> EncodeJpeg2000(
        ReadOnlySpan<byte> rgb,
        int width,
        int height
    )
    {
        var valid = PortraitBitmap.Validate(rgb.Length, width, height);
        return valid.IsFailure
            ? valid.Error.Fail<byte[]>()
            : Jpeg2000Encoder.Encode(rgb.ToArray(), width, height);
    }

    /// <summary>Encodes a lossless PNG preview of the prepared pixels.</summary>
    /// <param name="rgb">Tightly packed, opaque sRGB pixels.</param>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <returns>Owned PNG bytes, or a failure for invalid pixels.</returns>
    public static Result<byte[], FacialError> EncodePng(
        ReadOnlySpan<byte> rgb,
        int width,
        int height
    )
    {
        var valid = PortraitBitmap.Validate(rgb.Length, width, height);
        return valid.IsFailure ? valid.Error.Fail<byte[]>() : Png(rgb.ToArray(), width, height);
    }

    private static Result<byte[], FacialError> Png(byte[] rgb, int width, int height) =>
        FacialEdge.Try(
            () =>
            {
                using var bitmap = PortraitBitmap.Create(rgb, width, height);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                return data.ToArray();
            },
            FacialErrorCodes.ImageProcessingFailed,
            "The image library failed to encode the preview."
        );
}
