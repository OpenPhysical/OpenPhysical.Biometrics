// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging;

/// <summary>In-place colour edits that return the image for composition with Bind.</summary>
public static class RgbImageColor
{
    /// <summary>Replaces every pixel with a selected colour.</summary>
    /// <param name="image">Image to fill in place.</param>
    /// <param name="color">Replacement colour.</param>
    /// <returns>The filled image, or a disposed failure.</returns>
    public static Result<RgbImage, ImagingError> Fill(this RgbImage image, Color color) =>
        image.Fill(color, image.Bounds);

    /// <summary>Replaces pixels in a rectangle with a selected colour.</summary>
    /// <param name="image">Image to fill in place.</param>
    /// <param name="color">Replacement colour.</param>
    /// <param name="rectangle">Rectangle within the image bounds.</param>
    /// <returns>The filled image, or <see cref="ImagingErrorCodes.FillOutOfRange"/>.</returns>
    public static Result<RgbImage, ImagingError> Fill(
        this RgbImage image,
        Color color,
        Rectangle rectangle
    ) =>
        image
            .Active()
            .Ensure(
                active =>
                    rectangle.X >= 0
                    && rectangle.Y >= 0
                    && rectangle.Width >= 0
                    && rectangle.Height >= 0
                    && (long)rectangle.X + rectangle.Width <= active.Width
                    && (long)rectangle.Y + rectangle.Height <= active.Height,
                ImagingErrors.FillOutOfRange()
            )
            .Tap(active => FillRows(active, (RgbaPixel)color, rectangle));

    /// <summary>Converts all colour samples to BT.709 grayscale while retaining alpha.</summary>
    /// <param name="image">Image to convert in place.</param>
    /// <returns>The converted image, or a disposed failure.</returns>
    public static Result<RgbImage, ImagingError> Grayscale(this RgbImage image) =>
        image.Active().Tap(active => ConvertToGray(active.Pixels));

    private static RgbImage FillRows(RgbImage image, RgbaPixel value, Rectangle rectangle)
    {
        for (var y = rectangle.Top; y < rectangle.Bottom; y++)
            image.Pixels.AsSpan(y * image.Width + rectangle.X, rectangle.Width).Fill(value);
        return image;
    }

    private static RgbaPixel[] ConvertToGray(RgbaPixel[] pixels)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            var value = pixels[i];
            var gray = (byte)
                Math.Clamp(
                    (int)Math.Round(0.2126 * value.R + 0.7152 * value.G + 0.0722 * value.B),
                    0,
                    255
                );
            pixels[i] = new(gray, gray, gray, value.A);
        }
        return pixels;
    }
}
