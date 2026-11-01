// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using SkiaSharp;

namespace OpenPhysical.Biometrics.Imaging;

// Skia canvas callbacks are void by SkiaSharp's design; they run only inside ImagingGuard.
internal static class SkiaBridge
{
    internal static Result<SKBitmap, ImagingError> ToBitmap(RgbImage image) =>
        image
            .Active()
            .Bind(active =>
                ImagingGuard
                    .Try(
                        () =>
                            new SKBitmap(
                                new SKImageInfo(
                                    active.Width,
                                    active.Height,
                                    SKColorType.Rgba8888,
                                    SKAlphaType.Unpremul
                                )
                            ),
                        ImagingErrorCodes.SkiaFailure
                    )
                    .Bind(bitmap => CopyInto(bitmap, active))
            );

    internal static Result<T, ImagingError> WithBitmap<T>(
        RgbImage image,
        Func<SKBitmap, Result<T, ImagingError>> use
    ) => ToBitmap(image).Bind(bitmap => UseAndDispose(bitmap, use));

    internal static RgbaPixel[] ReadPixels(SKBitmap bitmap)
    {
        var pixels = new RgbaPixel[bitmap.Width * bitmap.Height];
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var value = bitmap.GetPixel(x, y);
            pixels[y * bitmap.Width + x] = new(value.Red, value.Green, value.Blue, value.Alpha);
        }
        return pixels;
    }

    internal static Result<RgbImage, ImagingError> Render(
        RgbImage image,
        int width,
        int height,
        Action<SKCanvas, SKImage> draw
    ) =>
        image
            .Active()
            .Ensure(_ => RgbImage.DimensionsValid(width, height), ImagingErrors.InvalidDimensions())
            .Bind(active =>
                WithBitmap(
                    active,
                    bitmap =>
                        ImagingGuard.Try(
                            () => Rasterize(bitmap, width, height, draw),
                            ImagingErrorCodes.SkiaFailure
                        )
                )
            )
            .Map(pixels => image.Replace(pixels, width, height));

    private static Result<SKBitmap, ImagingError> CopyInto(SKBitmap bitmap, RgbImage image) =>
        ImagingGuard
            .Try(() => Transfer(bitmap, image), ImagingErrorCodes.SkiaFailure)
            .TapError(_ => bitmap.Dispose());

    private static SKBitmap Transfer(SKBitmap bitmap, RgbImage image)
    {
        var bytes = RgbImageBytes.Pack(image.Pixels, 4);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bitmap.GetPixels(), bytes.Length);
            return bitmap;
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private static Result<T, ImagingError> UseAndDispose<T>(
        SKBitmap bitmap,
        Func<SKBitmap, Result<T, ImagingError>> use
    )
    {
        using (bitmap)
            return use(bitmap);
    }

    private static RgbaPixel[] Rasterize(
        SKBitmap source,
        int width,
        int height,
        Action<SKCanvas, SKImage> draw
    )
    {
        using var image = SKImage.FromBitmap(source);
        using var target = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)
        );
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            draw(canvas, image);
            canvas.Flush();
        }
        return ReadPixels(target);
    }
}
