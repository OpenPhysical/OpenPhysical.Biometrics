// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Converts between images and tightly packed sample buffers.</summary>
public static class RgbImageBytes
{
    /// <summary>Creates an image from tightly packed RGB samples.</summary>
    /// <param name="data">Exactly three bytes per pixel in row-major order.</param>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <returns>An independent image, <see cref="ImagingErrorCodes.InvalidDimensions"/>, or <see cref="ImagingErrorCodes.RgbLengthMismatch"/>.</returns>
    public static Result<RgbImage, ImagingError> FromRgbBytes(
        ReadOnlySpan<byte> data,
        int width,
        int height
    )
    {
        if (!RgbImage.DimensionsValid(width, height))
            return ImagingErrors.InvalidDimensions().Fail<RgbImage>();
        if (data.Length != width * height * 3)
            return ImagingErrors.RgbLengthMismatch().Fail<RgbImage>();
        var pixels = new RgbaPixel[width * height];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = new(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
        return Result.Success<RgbImage, ImagingError>(RgbImage.Adopt(pixels, width, height));
    }

    /// <summary>Owns opaque sRGB samples from top-down BGRA rows, excluding row padding.</summary>
    /// <param name="data">Complete rows in blue, green, red, alpha order.</param>
    /// <param name="width">Positive source width, without resampling.</param>
    /// <param name="height">Positive source height.</param>
    /// <param name="rowBytes">Actual bytes between row starts.</param>
    /// <returns>An independent opaque image, or an invalid geometry/buffer failure.</returns>
    public static Result<RgbImage, ImagingError> FromBgraBytes(
        ReadOnlySpan<byte> data,
        int width,
        int height,
        int rowBytes
    )
    {
        if (!RgbImage.DimensionsValid(width, height))
            return ImagingErrors.InvalidDimensions();
        if (rowBytes < (long)width * 4 || data.Length < (long)rowBytes * height)
            return ImagingErrors.RgbLengthMismatch();
        var pixels = new RgbaPixel[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = y * rowBytes + x * 4;
            pixels[y * width + x] = new(data[offset + 2], data[offset + 1], data[offset]);
        }
        return RgbImage.Adopt(pixels, width, height);
    }

    /// <summary>Copies tightly packed RGBA samples in row-major order.</summary>
    /// <param name="image">Image to copy.</param>
    /// <returns>Four bytes per pixel owned by the caller, or a disposed failure.</returns>
    public static Result<byte[], ImagingError> CopyRgbaBytes(this RgbImage image) =>
        image.Active().Map(active => Pack(active.Pixels, 4));

    /// <summary>Copies tightly packed RGB samples in row-major order.</summary>
    /// <param name="image">Image to copy.</param>
    /// <returns>Three colour bytes per pixel owned by the caller, or a disposed failure.</returns>
    public static Result<byte[], ImagingError> CopyRgbBytes(this RgbImage image) =>
        image.Active().Map(active => Pack(active.Pixels, 3));

    internal static byte[] Pack(RgbaPixel[] pixels, int stride)
    {
        var result = new byte[pixels.Length * stride];
        for (var i = 0; i < pixels.Length; i++)
        {
            result[i * stride] = pixels[i].R;
            result[i * stride + 1] = pixels[i].G;
            result[i * stride + 2] = pixels[i].B;
            if (stride == 4)
                result[i * stride + 3] = pixels[i].A;
        }
        return result;
    }
}
