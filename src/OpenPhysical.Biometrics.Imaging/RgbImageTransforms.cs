// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;
using CSharpFunctionalExtensions;
using SkiaSharp;

namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Geometric transformations that mutate an image and return it for composition with Bind.</summary>
public static class RgbImageTransforms
{
    private static readonly SKSamplingOptions Cubic = new(SKCubicResampler.Mitchell);

    /// <summary>Crops the image to a rectangle within its current bounds.</summary>
    /// <param name="image">Image to crop in place.</param>
    /// <param name="rectangle">Positive crop rectangle in source pixels.</param>
    /// <returns>The cropped image, or <see cref="ImagingErrorCodes.CropOutOfRange"/>.</returns>
    public static Result<RgbImage, ImagingError> Crop(this RgbImage image, Rectangle rectangle) =>
        image
            .Active()
            .Ensure(
                active =>
                    rectangle.Width > 0
                    && rectangle.Height > 0
                    && rectangle.X >= 0
                    && rectangle.Y >= 0
                    && (long)rectangle.X + rectangle.Width <= active.Width
                    && (long)rectangle.Y + rectangle.Height <= active.Height,
                ImagingErrors.CropOutOfRange()
            )
            .Map(active => CopyRegion(active, rectangle));

    /// <summary>Resizes with cubic filtering, optionally filling and cropping to preserve aspect ratio.</summary>
    /// <param name="image">Image to resize in place.</param>
    /// <param name="width">Output width in pixels.</param>
    /// <param name="height">Output height in pixels.</param>
    /// <param name="fillCrop">Preserves aspect ratio and crops equally at opposing edges when true.</param>
    /// <returns>The resized image, or a dimension failure.</returns>
    public static Result<RgbImage, ImagingError> Resize(
        this RgbImage image,
        int width,
        int height,
        bool fillCrop = false
    )
    {
        var scale = fillCrop
            ? Math.Max((float)width / image.Width, (float)height / image.Height)
            : 0;
        var drawWidth = fillCrop ? image.Width * scale : width;
        var drawHeight = fillCrop ? image.Height * scale : height;
        var target = SKRect.Create(
            (width - drawWidth) / 2,
            (height - drawHeight) / 2,
            drawWidth,
            drawHeight
        );
        return SkiaBridge.Render(
            image,
            width,
            height,
            (canvas, source) => canvas.DrawImage(source, target, Cubic)
        );
    }

    /// <summary>Resizes to explicit pixel dimensions using cubic filtering.</summary>
    /// <param name="image">Image to resize in place.</param>
    /// <param name="size">Output pixel dimensions.</param>
    /// <returns>The resized image, or a dimension failure.</returns>
    public static Result<RgbImage, ImagingError> Resize(this RgbImage image, Size size) =>
        image.Resize(size.Width, size.Height);

    /// <summary>Rotates clockwise in image coordinates and expands the canvas to retain the complete image.</summary>
    /// <param name="image">Image to rotate in place.</param>
    /// <param name="degrees">Finite clockwise rotation in degrees.</param>
    /// <returns>The rotated image, or <see cref="ImagingErrorCodes.RotationNotFinite"/>.</returns>
    public static Result<RgbImage, ImagingError> Rotate(this RgbImage image, float degrees) =>
        image
            .Active()
            .Ensure(_ => float.IsFinite(degrees), ImagingErrors.RotationNotFinite())
            .Bind(active => RenderRotation(active, degrees % 360));

    /// <summary>Reflects the image across its vertical centre line.</summary>
    /// <param name="image">Image to reflect in place.</param>
    /// <returns>The reflected image, or a disposed failure.</returns>
    public static Result<RgbImage, ImagingError> FlipHorizontal(this RgbImage image) =>
        image.Active().Tap(active => FlipRows(active));

    /// <summary>Places the image at the centre of a larger transparent canvas.</summary>
    /// <param name="image">Image to pad in place.</param>
    /// <param name="width">Canvas width, at least the current width.</param>
    /// <param name="height">Canvas height, at least the current height.</param>
    /// <returns>The padded image, or <see cref="ImagingErrorCodes.PaddingTooSmall"/>.</returns>
    public static Result<RgbImage, ImagingError> Pad(this RgbImage image, int width, int height)
    {
        var x = (width - image.Width) / 2;
        var y = (height - image.Height) / 2;
        return image
            .Active()
            .Ensure(
                active => width >= active.Width && height >= active.Height,
                ImagingErrors.PaddingTooSmall()
            )
            .Bind(active =>
                SkiaBridge.Render(
                    active,
                    width,
                    height,
                    (canvas, source) => canvas.DrawImage(source, x, y, SKSamplingOptions.Default)
                )
            );
    }

    /// <summary>Applies a Gaussian blur in pixel units.</summary>
    /// <param name="image">Image to filter in place.</param>
    /// <param name="sigma">Positive Gaussian standard deviation up to 100.</param>
    /// <returns>The filtered image, or <see cref="ImagingErrorCodes.BlurSigmaOutOfRange"/>.</returns>
    public static Result<RgbImage, ImagingError> GaussianBlur(this RgbImage image, float sigma) =>
        image
            .Active()
            .Ensure(
                _ => float.IsFinite(sigma) && sigma > 0 && sigma <= 100,
                ImagingErrors.BlurSigmaOutOfRange()
            )
            .Bind(active =>
                ImagingGuard.TryBind(() => RenderBlur(active, sigma), ImagingErrorCodes.SkiaFailure)
            );

    private static RgbImage CopyRegion(RgbImage image, Rectangle rectangle)
    {
        var copy = new RgbaPixel[rectangle.Width * rectangle.Height];
        for (var y = 0; y < rectangle.Height; y++)
            image
                .Pixels.AsSpan((rectangle.Y + y) * image.Width + rectangle.X, rectangle.Width)
                .CopyTo(copy.AsSpan(y * rectangle.Width));
        return image.Replace(copy, rectangle.Width, rectangle.Height);
    }

    private static RgbImage FlipRows(RgbImage image)
    {
        for (var y = 0; y < image.Height; y++)
            image.Pixels.AsSpan(y * image.Width, image.Width).Reverse();
        return image;
    }

    private static Result<RgbImage, ImagingError> RenderRotation(RgbImage image, float degrees)
    {
        var radians = degrees * Math.PI / 180;
        var cosine = Math.Abs(Math.Cos(radians));
        var sine = Math.Abs(Math.Sin(radians));
        if (cosine < 1e-10)
            cosine = 0;
        if (sine < 1e-10)
            sine = 0;
        var width = (int)Math.Ceiling(image.Width * cosine + image.Height * sine);
        var height = (int)Math.Ceiling(image.Width * sine + image.Height * cosine);
        var sourceWidth = image.Width;
        var sourceHeight = image.Height;
        return SkiaBridge.Render(
            image,
            width,
            height,
            (canvas, source) =>
            {
                canvas.Translate(width / 2f, height / 2f);
                canvas.RotateDegrees(degrees);
                canvas.DrawImage(source, -sourceWidth / 2f, -sourceHeight / 2f, Cubic);
            }
        );
    }

    private static Result<RgbImage, ImagingError> RenderBlur(RgbImage image, float sigma)
    {
        using var filter = SKImageFilter.CreateBlur(sigma, sigma);
        using var paint = new SKPaint { ImageFilter = filter };
        return SkiaBridge.Render(
            image,
            image.Width,
            image.Height,
            (canvas, source) => canvas.DrawImage(source, 0, 0, SKSamplingOptions.Default, paint)
        );
    }
}
