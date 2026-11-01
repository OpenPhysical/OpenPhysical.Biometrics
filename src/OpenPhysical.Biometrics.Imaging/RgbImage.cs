// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Owns mutable sRGB pixels; transformations live in the <c>RgbImage*</c> extension classes.</summary>
/// <remarks>
/// Operations that can fail return <see cref="Result{T,E}"/> with an <see cref="ImagingError"/>.
/// Transformations mutate and return this instance so they compose with <c>Bind</c>.
/// Dispose clears the pixels, and operations on one instance require exclusive access.
/// </remarks>
public sealed class RgbImage : IDisposable
{
    internal const int MaximumPixels = 64 * 1024 * 1024;
    private RgbaPixel[] pixels;
    private bool disposed;

    private RgbImage(int width, int height, RgbaPixel[] storage)
    {
        Width = width;
        Height = height;
        pixels = storage;
    }

    /// <summary>Gets the pixel width.</summary>
    public int Width { get; private set; }

    /// <summary>Gets the pixel height.</summary>
    public int Height { get; private set; }

    /// <summary>Gets the complete image bounds.</summary>
    public Rectangle Bounds => new(0, 0, Width, Height);

    internal bool IsDisposed => disposed;

    internal RgbaPixel[] Pixels => pixels;

    /// <summary>Creates a transparent image with explicit pixel dimensions.</summary>
    /// <param name="width">Positive pixel width.</param>
    /// <param name="height">Positive pixel height.</param>
    /// <returns>The image, or <see cref="ImagingErrorCodes.InvalidDimensions"/>.</returns>
    public static Result<RgbImage, ImagingError> Create(int width, int height) =>
        UnitResult
            .SuccessIf(DimensionsValid(width, height), ImagingErrors.InvalidDimensions())
            .Map(() => new RgbImage(width, height, new RgbaPixel[width * height]));

    /// <summary>Creates an image filled with an explicit colour.</summary>
    /// <param name="width">Positive pixel width.</param>
    /// <param name="height">Positive pixel height.</param>
    /// <param name="background">Initial colour for every pixel.</param>
    /// <returns>The image, or <see cref="ImagingErrorCodes.InvalidDimensions"/>.</returns>
    public static Result<RgbImage, ImagingError> Create(int width, int height, Color background) =>
        Create(width, height, (RgbaPixel)background);

    /// <summary>Creates an image filled with explicit pixel samples.</summary>
    /// <param name="width">Positive pixel width.</param>
    /// <param name="height">Positive pixel height.</param>
    /// <param name="background">Initial pixel value.</param>
    /// <returns>The image, or <see cref="ImagingErrorCodes.InvalidDimensions"/>.</returns>
    public static Result<RgbImage, ImagingError> Create(
        int width,
        int height,
        RgbaPixel background
    ) => Create(width, height).Tap(image => Array.Fill(image.pixels, background));

    /// <summary>Reads one pixel in image coordinates.</summary>
    /// <param name="x">Zero-based column index.</param>
    /// <param name="y">Zero-based row index.</param>
    /// <returns>The straight-alpha sRGB pixel, or a disposed or out-of-range failure.</returns>
    public Result<RgbaPixel, ImagingError> GetPixel(int x, int y) =>
        Locate(x, y).Map(index => pixels[index]);

    /// <summary>Replaces one pixel in image coordinates.</summary>
    /// <param name="x">Zero-based column index.</param>
    /// <param name="y">Zero-based row index.</param>
    /// <param name="value">Replacement pixel.</param>
    /// <returns>Success, or a disposed or out-of-range failure.</returns>
    public UnitResult<ImagingError> SetPixel(int x, int y, RgbaPixel value) =>
        Locate(x, y).Tap(index => pixels[index] = value).AsUnit();

    /// <summary>Gets mutable pixels for one row.</summary>
    /// <param name="y">Zero-based row index.</param>
    /// <param name="row">A managed span belonging to the image, or empty on failure.</param>
    /// <returns>True when the image is active and the row exists.</returns>
    public bool TryGetRowSpan(int y, out Span<RgbaPixel> row)
    {
        var found = !disposed && (uint)y < (uint)Height;
        row = found ? pixels.AsSpan(y * Width, Width) : default;
        return found;
    }

    /// <summary>Copies this image into an independent instance.</summary>
    /// <returns>An image owned by the caller, or a disposed failure.</returns>
    public Result<RgbImage, ImagingError> Clone() =>
        Active().Map(source => new RgbImage(Width, Height, (RgbaPixel[])source.pixels.Clone()));

    /// <summary>Copies this image and transforms the copy, disposing the copy if the transform fails.</summary>
    /// <param name="transform">Transform applied to the independent copy.</param>
    /// <returns>The transform result, owned by the caller.</returns>
    public Result<RgbImage, ImagingError> Clone(
        Func<RgbImage, Result<RgbImage, ImagingError>> transform
    ) => Clone().Bind(copy => transform(copy).TapError(_ => copy.Dispose()));

    /// <summary>Clears all managed pixel samples and releases this image.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        Array.Clear(pixels);
        disposed = true;
    }

    internal static bool DimensionsValid(long width, long height) =>
        width > 0
        && height > 0
        && width <= MaximumPixels
        && height <= MaximumPixels
        && width * height <= MaximumPixels;

    internal static RgbImage Adopt(RgbaPixel[] storage, int width, int height) =>
        new(width, height, storage);

    internal Result<RgbImage, ImagingError> Active() =>
        disposed
            ? ImagingErrors.Disposed().Fail<RgbImage>()
            : Result.Success<RgbImage, ImagingError>(this);

    internal RgbImage Replace(RgbaPixel[] replacement, int width, int height)
    {
        Array.Clear(pixels);
        pixels = replacement;
        Width = width;
        Height = height;
        return this;
    }

    private Result<int, ImagingError> Locate(int x, int y) =>
        Active()
            .Ensure(
                _ => (uint)x < (uint)Width && (uint)y < (uint)Height,
                ImagingErrors.PixelOutOfRange()
            )
            .Map(_ => y * Width + x);
}
