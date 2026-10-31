namespace OpenPhysical.Domains.Biometrics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;

/// <summary>
/// Represents immutable image dimensions (width and height).
/// </summary>
/// <remarks>
/// This value object encapsulates image dimensions with validation to ensure
/// both width and height are positive values.
/// </remarks>
public sealed record ImageDimensions
{
    /// <summary>
    /// Gets the width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the total number of pixels (Width × Height).
    /// </summary>
    public int PixelCount => Width * Height;

    /// <summary>
    /// Gets the aspect ratio (Width / Height).
    /// </summary>
    public double AspectRatio => (double)Width / Height;

    /// <summary>
    /// Initializes a new instance of the ImageDimensions record.
    /// </summary>
    private ImageDimensions(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Creates new image dimensions with validation.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>A Result containing the ImageDimensions or an error.</returns>
    public static Result<ImageDimensions> Create(int width, int height) =>
        Result
            .Success()
            .Ensure(() => width > 0, "Width must be greater than zero")
            .Ensure(() => height > 0, "Height must be greater than zero")
            .Ensure(() => width <= 10000, "Width exceeds maximum allowed value (10000)")
            .Ensure(() => height <= 10000, "Height exceeds maximum allowed value (10000)")
            .Map(() => new ImageDimensions(width, height));

    /// <summary>
    /// Common image dimension standards.
    /// </summary>
    public static class Standards
    {
        /// <summary>
        /// PIV facial image dimensions (420×560).
        /// </summary>
        public static ImageDimensions Piv { get; } = new(420, 560);

        /// <summary>
        /// ICAO Type 10 passport photo dimensions (413×531).
        /// </summary>
        public static ImageDimensions IcaoType10 { get; } = new(413, 531);

        /// <summary>
        /// ISO/IEC 19794-5 token frontal image dimensions (240×320).
        /// </summary>
        public static ImageDimensions Iso19794Part5Token { get; } = new(240, 320);

        /// <summary>
        /// VGA dimensions (640×480).
        /// </summary>
        public static ImageDimensions Vga { get; } = new(640, 480);

        /// <summary>
        /// HD dimensions (1920×1080).
        /// </summary>
        public static ImageDimensions Hd { get; } = new(1920, 1080);
    }

    /// <summary>
    /// Scales the dimensions by a factor while maintaining aspect ratio.
    /// </summary>
    /// <param name="scaleFactor">The scale factor to apply.</param>
    /// <returns>A Result containing the scaled dimensions or an error.</returns>
    public Result<ImageDimensions> Scale(double scaleFactor)
    {
        if (scaleFactor <= 0)
            return Result.Failure<ImageDimensions>("Scale factor must be greater than zero");

        var newWidth = (int)Math.Round(Width * scaleFactor);
        var newHeight = (int)Math.Round(Height * scaleFactor);

        return Create(newWidth, newHeight);
    }

    /// <summary>
    /// Calculates the scale factor needed to fit within target dimensions while maintaining aspect ratio.
    /// </summary>
    /// <param name="target">The target dimensions to fit within.</param>
    /// <returns>The scale factor to apply.</returns>
    public double GetScaleToFit(ImageDimensions target)
    {
        var widthScale = (double)target.Width / Width;
        var heightScale = (double)target.Height / Height;
        return Math.Min(widthScale, heightScale);
    }

    /// <summary>
    /// Determines if these dimensions match another set of dimensions.
    /// </summary>
    /// <param name="other">The dimensions to compare with.</param>
    /// <returns>True if dimensions match exactly.</returns>
    public bool Matches(ImageDimensions other) =>
        other != null && Width == other.Width && Height == other.Height;

    /// <summary>
    /// Returns the string representation of these dimensions.
    /// </summary>
    public override string ToString() => $"{Width}×{Height}";
}
