// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;
using CSharpFunctionalExtensions;
using SkiaSharp;

namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Alpha compositing operations that mutate an image and return it for composition with Bind.</summary>
public static class RgbImageCompositing
{
    /// <summary>Composites another image onto this one at an explicit position.</summary>
    /// <param name="image">Destination image, changed in place.</param>
    /// <param name="source">Source image retained by its caller.</param>
    /// <param name="position">Top-left destination position.</param>
    /// <param name="opacity">Additional opacity from zero through one.</param>
    /// <returns>The composited image, or <see cref="ImagingErrorCodes.OpacityOutOfRange"/>.</returns>
    public static Result<RgbImage, ImagingError> DrawImage(
        this RgbImage image,
        RgbImage source,
        Point position,
        float opacity = 1
    ) =>
        UnitResult
            .SuccessIf(
                float.IsFinite(opacity) && opacity >= 0 && opacity <= 1,
                ImagingErrors.OpacityOutOfRange()
            )
            .Bind(() =>
                SkiaBridge.WithBitmap(
                    source,
                    bitmap =>
                        ImagingGuard.TryBind(
                            () => Overlay(image, bitmap, position, opacity),
                            ImagingErrorCodes.SkiaFailure
                        )
                )
            );

    /// <summary>Composites the image over an explicit background colour.</summary>
    /// <param name="image">Image to composite in place.</param>
    /// <param name="color">Background colour.</param>
    /// <returns>The composited image, or a disposed failure.</returns>
    public static Result<RgbImage, ImagingError> BackgroundColor(
        this RgbImage image,
        Color color
    ) =>
        SkiaBridge.Render(
            image,
            image.Width,
            image.Height,
            (canvas, source) =>
            {
                canvas.Clear(new SKColor(color.R, color.G, color.B, color.A));
                canvas.DrawImage(source, 0, 0, SKSamplingOptions.Default);
            }
        );

    private static Result<RgbImage, ImagingError> Overlay(
        RgbImage image,
        SKBitmap bitmap,
        Point position,
        float opacity
    )
    {
        using var overlay = SKImage.FromBitmap(bitmap);
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha((byte)Math.Round(opacity * 255)),
        };
        return SkiaBridge.Render(
            image,
            image.Width,
            image.Height,
            (canvas, source) =>
            {
                canvas.DrawImage(source, 0, 0, SKSamplingOptions.Default);
                canvas.DrawImage(overlay, position.X, position.Y, SKSamplingOptions.Default, paint);
            }
        );
    }
}
