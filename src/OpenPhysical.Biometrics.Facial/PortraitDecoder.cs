// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using SkiaSharp;

// Decodes the original capture to opaque sRGB pixels with EXIF orientation applied once.
internal static class PortraitDecoder
{
    private const int MaxEncodedBytes = 64 * 1024 * 1024;
    private const long MaxPixels = 32 * 1024 * 1024;

    internal static Result<PortraitPixels, FacialError> Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length is < 1 or > MaxEncodedBytes)
            return FacialErrors.EncodedImageSize().Fail<PortraitPixels>();
        using var data = SKData.CreateCopy(encoded);
        return FacialEdge.TryBind(
            () => Open(data),
            FacialErrorCodes.ImageProcessingFailed,
            "The image library failed to decode the portrait."
        );
    }

    private static Result<PortraitPixels, FacialError> Open(SKData data) =>
        Maybe
            .From(SKCodec.Create(data))
            .ToResult(FacialErrors.UnsupportedImage())
            .Bind(codec =>
            {
                using (codec)
                    return Rasterize(codec);
            });

    private static Result<PortraitPixels, FacialError> Rasterize(SKCodec codec)
    {
        var w = codec.Info.Width;
        var h = codec.Info.Height;
        if (w < 1 || h < 1 || (long)w * h > MaxPixels)
            return FacialErrors.ImageDimensionsExceeded().Fail<PortraitPixels>();
        using var color = SKColorSpace.CreateSrgb();
        using var bitmap = new SKBitmap(
            new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul, color)
        );
        return codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success
            ? Orient((int)codec.EncodedOrigin, bitmap, w, h)
            : FacialErrors.ImageDecodeFailed().Fail<PortraitPixels>();
    }

    private static Result<PortraitPixels, FacialError> Orient(
        int origin,
        SKBitmap bitmap,
        int w,
        int h
    )
    {
        var rotated = origin >= 5;
        var width = rotated ? h : w;
        var height = rotated ? w : h;
        var rgb = new byte[width * height * 3];
        var rgba = bitmap.GetPixelSpan();
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var (dx, dy) = Destination(origin, w, h, x, y);
            var i = (y * w + x) * 4;
            var o = (dy * width + dx) * 3;
            if (rgba[i + 3] != 255)
                return FacialErrors.ImageNotOpaque().Fail<PortraitPixels>();
            for (var c = 0; c < 3; c++)
                rgb[o + c] = rgba[i + c];
        }
        return new PortraitPixels(width, height, rgb).Succeed();
    }

    // EXIF orientation is applied once, before any coordinates are exposed.
    private static (int X, int Y) Destination(int origin, int w, int h, int x, int y) =>
        origin switch
        {
            2 => (w - 1 - x, y),
            3 => (w - 1 - x, h - 1 - y),
            4 => (x, h - 1 - y),
            5 => (y, x),
            6 => (h - 1 - y, x),
            7 => (h - 1 - y, w - 1 - x),
            8 => (y, w - 1 - x),
            _ => (x, y),
        };
}
