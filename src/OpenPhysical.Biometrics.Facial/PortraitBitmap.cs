// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using SkiaSharp;

// Validates pixel buffers and materializes them as opaque Skia bitmaps.
internal static class PortraitBitmap
{
    internal static UnitResult<FacialError> Validate(int length, int width, int height) =>
        width is < 1 or > 4096 || height is < 1 or > 4096 || length != (long)width * height * 3
            ? UnitResult.Failure(FacialErrors.InvalidPixelBuffer())
            : UnitResult.Success<FacialError>();

    // The caller owns and disposes the returned bitmap.
    internal static SKBitmap Create(byte[] rgb, int width, int height)
    {
        using var color = SKColorSpace.CreateSrgb();
        var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque, color)
        );
        var pixels = bitmap.GetPixelSpan();
        for (var i = 0; i < width * height; i++)
        {
            for (var c = 0; c < 3; c++)
                pixels[i * 4 + c] = rgb[i * 3 + c];
            pixels[i * 4 + 3] = 255;
        }
        return bitmap;
    }
}
