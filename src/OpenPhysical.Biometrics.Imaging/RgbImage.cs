using System.Drawing;
using SkiaSharp;

namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Owns mutable sRGB pixels and deterministic image transformations.</summary>
/// <remarks>Pixel spans refer to managed memory. Dispose clears the image, and operations on one instance require exclusive access.</remarks>
public sealed class RgbImage : IDisposable
{
    private const int MaximumPixels = 64 * 1024 * 1024;
    private const int MaximumEncodedBytes = 256 * 1024 * 1024;
    private RgbaPixel[] pixels;
    private bool disposed;

    /// <summary>Creates a transparent image with explicit pixel dimensions.</summary>
    /// <param name="width">Positive pixel width.</param>
    /// <param name="height">Positive pixel height.</param>
    public RgbImage(int width, int height)
    {
        ValidateDimensions(width, height);
        Width = width;
        Height = height;
        pixels = new RgbaPixel[checked(width * height)];
    }

    /// <summary>Creates an image filled with an explicit colour.</summary>
    /// <param name="width">Positive pixel width.</param>
    /// <param name="height">Positive pixel height.</param>
    /// <param name="background">Initial colour for every pixel.</param>
    public RgbImage(int width, int height, Color background)
        : this(width, height) =>
        Array.Fill(pixels, new RgbaPixel(background.R, background.G, background.B, background.A));

    /// <summary>Creates an image filled with explicit pixel samples.</summary>
    /// <param name="width">Positive pixel width.</param>
    /// <param name="height">Positive pixel height.</param>
    /// <param name="background">Initial pixel value.</param>
    public RgbImage(int width, int height, RgbaPixel background)
        : this(width, height) => Array.Fill(pixels, background);

    /// <summary>Gets the pixel width.</summary>
    public int Width { get; private set; }

    /// <summary>Gets the pixel height.</summary>
    public int Height { get; private set; }

    /// <summary>Gets the complete image bounds.</summary>
    public Rectangle Bounds => new(0, 0, Width, Height);

    /// <summary>Gets or replaces one pixel in image coordinates.</summary>
    /// <param name="x">Zero-based column index.</param>
    /// <param name="y">Zero-based row index.</param>
    /// <returns>The straight-alpha sRGB pixel at that position.</returns>
    public RgbaPixel this[int x, int y]
    {
        get
        {
            ValidatePosition(x, y);
            return pixels[y * Width + x];
        }
        set
        {
            ValidatePosition(x, y);
            pixels[y * Width + x] = value;
        }
    }

    /// <summary>Gets mutable pixels for one row.</summary>
    /// <param name="y">Zero-based row index.</param>
    /// <returns>A managed span valid independently of native Skia allocations.</returns>
    public Span<RgbaPixel> GetRowSpan(int y)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if ((uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(y));
        return pixels.AsSpan(y * Width, Width);
    }

    /// <summary>Runs a scoped operation over the managed pixel rows.</summary>
    /// <param name="operation">Operation with exclusive access to this image.</param>
    public void ProcessPixelRows(Action<PixelRows> operation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(operation);
        operation(new(this));
    }

    /// <summary>Copies this image and optionally transforms the copy.</summary>
    /// <param name="transform">Transform applied to the independent copy.</param>
    /// <returns>An image owned by the caller.</returns>
    public RgbImage Clone(Action<RgbImage>? transform = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var result = new RgbImage(Width, Height);
        pixels.CopyTo(result.pixels, 0);
        try
        {
            transform?.Invoke(result);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>Applies an explicit sequence of transformations to this instance.</summary>
    /// <param name="transform">Transformation sequence with exclusive access.</param>
    public void Mutate(Action<RgbImage> transform)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(transform);
        transform(this);
    }

    /// <summary>Crops the image to a rectangle within its current bounds.</summary>
    /// <param name="rectangle">Positive crop rectangle in source pixels.</param>
    /// <returns>This image after cropping.</returns>
    public RgbImage Crop(Rectangle rectangle)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (
            rectangle.Width <= 0
            || rectangle.Height <= 0
            || rectangle.X < 0
            || rectangle.Y < 0
            || (long)rectangle.X + rectangle.Width > Width
            || (long)rectangle.Y + rectangle.Height > Height
        )
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        var copy = new RgbaPixel[checked(rectangle.Width * rectangle.Height)];
        for (var y = 0; y < rectangle.Height; y++)
            pixels
                .AsSpan((rectangle.Y + y) * Width + rectangle.X, rectangle.Width)
                .CopyTo(copy.AsSpan(y * rectangle.Width));
        Replace(copy, rectangle.Width, rectangle.Height);
        return this;
    }

    /// <summary>Resizes with cubic filtering, optionally filling and cropping to preserve aspect ratio.</summary>
    /// <param name="width">Output width in pixels.</param>
    /// <param name="height">Output height in pixels.</param>
    /// <param name="fillCrop">Preserves aspect ratio and crops equally at opposing edges when true.</param>
    /// <returns>This image after resizing.</returns>
    public RgbImage Resize(int width, int height, bool fillCrop = false)
    {
        ValidateDimensions(width, height);
        var scale = fillCrop ? Math.Max((float)width / Width, (float)height / Height) : 0;
        var drawWidth = fillCrop ? Width * scale : width;
        var drawHeight = fillCrop ? Height * scale : height;
        return Render(
            width,
            height,
            (canvas, image) =>
                canvas.DrawImage(
                    image,
                    SKRect.Create(
                        (width - drawWidth) / 2,
                        (height - drawHeight) / 2,
                        drawWidth,
                        drawHeight
                    ),
                    new SKSamplingOptions(SKCubicResampler.Mitchell)
                )
        );
    }

    /// <summary>Resizes to explicit pixel dimensions using cubic filtering.</summary>
    /// <param name="size">Output pixel dimensions.</param>
    /// <returns>This image after resizing.</returns>
    public RgbImage Resize(Size size) => Resize(size.Width, size.Height);

    /// <summary>Rotates clockwise in image coordinates and expands the canvas to retain the complete image.</summary>
    /// <param name="degrees">Finite clockwise rotation in degrees.</param>
    /// <returns>This image after rotation.</returns>
    public RgbImage Rotate(float degrees)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!float.IsFinite(degrees))
            throw new ArgumentOutOfRangeException(nameof(degrees));
        degrees %= 360;
        var radians = degrees * Math.PI / 180;
        var cosine = Math.Abs(Math.Cos(radians));
        var sine = Math.Abs(Math.Sin(radians));
        if (cosine < 1e-10)
            cosine = 0;
        if (sine < 1e-10)
            sine = 0;
        var width = (int)Math.Ceiling(Width * cosine + Height * sine);
        var height = (int)Math.Ceiling(Width * sine + Height * cosine);
        var sourceWidth = Width;
        var sourceHeight = Height;
        return Render(
            width,
            height,
            (canvas, image) =>
            {
                canvas.Translate(width / 2f, height / 2f);
                canvas.RotateDegrees(degrees);
                canvas.DrawImage(
                    image,
                    -sourceWidth / 2f,
                    -sourceHeight / 2f,
                    new SKSamplingOptions(SKCubicResampler.Mitchell)
                );
            }
        );
    }

    /// <summary>Reflects the image across its vertical centre line.</summary>
    /// <returns>This image after reflection.</returns>
    public RgbImage FlipHorizontal()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        for (var y = 0; y < Height; y++)
            GetRowSpan(y).Reverse();
        return this;
    }

    /// <summary>Composites another image onto this one at an explicit position.</summary>
    /// <param name="source">Source image retained by its caller.</param>
    /// <param name="position">Top-left destination position.</param>
    /// <param name="opacity">Additional opacity from zero through one.</param>
    /// <returns>This image after compositing.</returns>
    public RgbImage DrawImage(RgbImage source, Point position, float opacity = 1)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!float.IsFinite(opacity) || opacity < 0 || opacity > 1)
            throw new ArgumentOutOfRangeException(nameof(opacity));
        using var bitmap = source.ToSkiaBitmap();
        using var overlay = SKImage.FromBitmap(bitmap);
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha((byte)Math.Round(opacity * 255)),
        };
        return Render(
            Width,
            Height,
            (canvas, image) =>
            {
                canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
                canvas.DrawImage(overlay, position.X, position.Y, SKSamplingOptions.Default, paint);
            }
        );
    }

    /// <summary>Composites the image over an explicit background colour.</summary>
    /// <param name="color">Background colour.</param>
    /// <returns>This image after background compositing.</returns>
    public RgbImage BackgroundColor(Color color) =>
        Render(
            Width,
            Height,
            (canvas, image) =>
            {
                canvas.Clear(new SKColor(color.R, color.G, color.B, color.A));
                canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
            }
        );

    /// <summary>Places the image at the centre of a larger transparent canvas.</summary>
    /// <param name="width">Canvas width, at least the current width.</param>
    /// <param name="height">Canvas height, at least the current height.</param>
    /// <returns>This image after padding.</returns>
    public RgbImage Pad(int width, int height)
    {
        if (width < Width || height < Height)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Padding dimensions must contain the original image."
            );
        var x = (width - Width) / 2;
        var y = (height - Height) / 2;
        return Render(
            width,
            height,
            (canvas, image) => canvas.DrawImage(image, x, y, SKSamplingOptions.Default)
        );
    }

    /// <summary>Replaces every pixel with a selected colour.</summary>
    /// <param name="color">Replacement colour.</param>
    /// <returns>This image after filling.</returns>
    public RgbImage Fill(Color color) => Fill(color, Bounds);

    /// <summary>Replaces pixels in a rectangle with a selected colour.</summary>
    /// <param name="color">Replacement colour.</param>
    /// <param name="rectangle">Rectangle within the image bounds.</param>
    /// <returns>This image after filling.</returns>
    public RgbImage Fill(Color color, Rectangle rectangle)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (
            rectangle.X < 0
            || rectangle.Y < 0
            || rectangle.Width < 0
            || rectangle.Height < 0
            || (long)rectangle.X + rectangle.Width > Width
            || (long)rectangle.Y + rectangle.Height > Height
        )
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        var value = new RgbaPixel(color.R, color.G, color.B, color.A);
        for (var y = rectangle.Top; y < rectangle.Bottom; y++)
            GetRowSpan(y).Slice(rectangle.X, rectangle.Width).Fill(value);
        return this;
    }

    /// <summary>Converts all colour samples to BT.709 grayscale while retaining alpha.</summary>
    /// <returns>This image after grayscale conversion.</returns>
    public RgbImage Grayscale()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
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
        return this;
    }

    /// <summary>Applies a Gaussian blur in pixel units.</summary>
    /// <param name="sigma">Positive Gaussian standard deviation.</param>
    /// <returns>This image after filtering.</returns>
    public RgbImage GaussianBlur(float sigma)
    {
        if (!float.IsFinite(sigma) || sigma <= 0 || sigma > 100)
            throw new ArgumentOutOfRangeException(nameof(sigma));
        using var filter = SKImageFilter.CreateBlur(sigma, sigma);
        using var paint = new SKPaint { ImageFilter = filter };
        return Render(
            Width,
            Height,
            (canvas, image) => canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default, paint)
        );
    }

    /// <summary>Copies tightly packed RGBA samples in row-major order.</summary>
    /// <returns>Four bytes per pixel, owned by the caller.</returns>
    public byte[] CopyRgbaBytes()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var result = new byte[checked(pixels.Length * 4)];
        for (var i = 0; i < pixels.Length; i++)
        {
            result[i * 4] = pixels[i].R;
            result[i * 4 + 1] = pixels[i].G;
            result[i * 4 + 2] = pixels[i].B;
            result[i * 4 + 3] = pixels[i].A;
        }
        return result;
    }

    /// <summary>Copies tightly packed RGB samples in row-major order.</summary>
    /// <returns>Three colour bytes per pixel, owned by the caller.</returns>
    public byte[] CopyRgbBytes()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var result = new byte[checked(pixels.Length * 3)];
        for (var i = 0; i < pixels.Length; i++)
        {
            result[i * 3] = pixels[i].R;
            result[i * 3 + 1] = pixels[i].G;
            result[i * 3 + 2] = pixels[i].B;
        }
        return result;
    }

    /// <summary>Creates an image from tightly packed RGB samples.</summary>
    /// <param name="data">Exactly three bytes per pixel in row-major order.</param>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <returns>An independent image owned by the caller.</returns>
    public static RgbImage FromRgbBytes(ReadOnlySpan<byte> data, int width, int height)
    {
        ValidateDimensions(width, height);
        if (data.Length != checked(width * height * 3))
            throw new ArgumentException(
                "RGB data length differs from the size required by the supplied dimensions.",
                nameof(data)
            );
        var result = new RgbImage(width, height);
        for (var i = 0; i < result.pixels.Length; i++)
            result.pixels[i] = new(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
        return result;
    }

    /// <summary>Decodes one bounded encoded image from memory.</summary>
    /// <param name="data">Encoded image bytes.</param>
    /// <returns>An independent image owned by the caller.</returns>
    public static RgbImage Load(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || data.Length > MaximumEncodedBytes)
            throw new ArgumentException(
                "Encoded image size is outside the supported range.",
                nameof(data)
            );
        if (BaselineTiff.IsTiff(data))
            return BaselineTiff.Decode(data);
        using var encoded = SKData.CreateCopy(data);
        using var codec =
            SKCodec.Create(encoded)
            ?? throw new ArgumentException(
                "The encoded image format is unsupported or malformed.",
                nameof(data)
            );
        ValidateDimensions(codec.Info.Width, codec.Info.Height);
        using var bitmap = new SKBitmap(
            new SKImageInfo(
                codec.Info.Width,
                codec.Info.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Unpremul
            )
        );
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new ArgumentException(
                "The encoded image is incomplete or malformed.",
                nameof(data)
            );
        return FromSkiaBitmap(bitmap);
    }

    /// <summary>Decodes an image file after checking its encoded size.</summary>
    /// <param name="path">Explicit input path.</param>
    /// <returns>An independent image owned by the caller.</returns>
    public static RgbImage Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>Decodes an image stream, reading at most the encoded-size limit.</summary>
    /// <param name="stream">Readable stream retained by its caller.</param>
    /// <returns>An independent image owned by the caller.</returns>
    public static RgbImage Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        try
        {
            int count;
            while ((count = stream.Read(chunk)) > 0)
            {
                if (buffer.Length + count > MaximumEncodedBytes)
                    throw new ArgumentException(
                        "The encoded image exceeds the supported size.",
                        nameof(stream)
                    );
                buffer.Write(chunk, 0, count);
            }
            return Load(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
        }
        finally
        {
            Array.Clear(chunk);
            Array.Clear(buffer.GetBuffer());
        }
    }

    /// <summary>Reads an image file with cancellation support before decoding its bounded contents.</summary>
    /// <param name="path">Explicit input path.</param>
    /// <param name="cancellationToken">Cancellation for file reads.</param>
    /// <returns>An independent image owned by the caller.</returns>
    public static async Task<RgbImage> LoadAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        if (new FileInfo(path).Length > MaximumEncodedBytes)
            throw new ArgumentException(
                "The encoded image exceeds the supported size.",
                nameof(path)
            );
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        try
        {
            int count;
            while (
                (count = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0
            )
            {
                if (buffer.Length + count > MaximumEncodedBytes)
                    throw new ArgumentException(
                        "The encoded image exceeds the supported size.",
                        nameof(path)
                    );
                buffer.Write(chunk, 0, count);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Load(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
        }
        finally
        {
            Array.Clear(chunk);
            Array.Clear(buffer.GetBuffer());
        }
    }

    /// <summary>Writes lossless PNG data to a caller-owned stream.</summary>
    /// <param name="stream">Writable destination stream.</param>
    public void SaveAsPng(Stream stream) => Encode(stream, SKEncodedImageFormat.Png, 100);

    /// <summary>Writes JPEG data to a caller-owned stream.</summary>
    /// <param name="stream">Writable destination stream.</param>
    /// <param name="quality">JPEG quality from one through 100.</param>
    public void SaveAsJpeg(Stream stream, int quality = 85) =>
        Encode(stream, SKEncodedImageFormat.Jpeg, quality);

    /// <summary>Writes an uncompressed RGB baseline TIFF with a 72 ppi display resolution.</summary>
    /// <remarks>The resolution tags describe the default display size of this pixel buffer.</remarks>
    /// <param name="stream">Writable destination stream.</param>
    public void SaveAsTiff(Stream stream)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        BaselineTiff.Encode(this, stream);
    }

    /// <summary>Writes lossless PNG data to an explicit file.</summary>
    /// <param name="path">Destination path.</param>
    public void SaveAsPng(string path)
    {
        using var stream = File.Create(path);
        SaveAsPng(stream);
    }

    /// <summary>Writes JPEG data to an explicit file.</summary>
    /// <param name="path">Destination path.</param>
    /// <param name="quality">JPEG quality from one through 100.</param>
    public void SaveAsJpeg(string path, int quality = 85)
    {
        using var stream = File.Create(path);
        SaveAsJpeg(stream, quality);
    }

    /// <summary>Writes PNG data and observes cancellation before encoding.</summary>
    /// <param name="stream">Writable destination stream.</param>
    /// <param name="cancellationToken">Cancellation checked before encoding.</param>
    /// <returns>A completed task when encoding finishes.</returns>
    public Task SaveAsPngAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveAsPng(stream);
        return Task.CompletedTask;
    }

    /// <summary>Writes JPEG data and observes cancellation before encoding.</summary>
    /// <param name="stream">Writable destination stream.</param>
    /// <param name="quality">JPEG quality from one through 100.</param>
    /// <param name="cancellationToken">Cancellation checked before encoding.</param>
    /// <returns>A completed task when encoding finishes.</returns>
    public Task SaveAsJpegAsync(
        Stream stream,
        int quality = 85,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveAsJpeg(stream, quality);
        return Task.CompletedTask;
    }

    /// <summary>Writes baseline TIFF data and observes cancellation before encoding.</summary>
    /// <param name="stream">Writable destination stream.</param>
    /// <param name="cancellationToken">Cancellation checked before encoding.</param>
    /// <returns>A completed task when encoding finishes.</returns>
    public Task SaveAsTiffAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveAsTiff(stream);
        return Task.CompletedTask;
    }

    /// <summary>Writes PNG data to an explicit file.</summary>
    /// <param name="path">Destination path.</param>
    /// <param name="cancellationToken">Cancellation checked before opening the file.</param>
    /// <returns>A completed task when encoding finishes.</returns>
    public Task SaveAsPngAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveAsPng(path);
        return Task.CompletedTask;
    }

    /// <summary>Writes JPEG data to an explicit file.</summary>
    /// <param name="path">Destination path.</param>
    /// <param name="quality">JPEG quality from one through 100.</param>
    /// <param name="cancellationToken">Cancellation checked before opening the file.</param>
    /// <returns>A completed task when encoding finishes.</returns>
    public Task SaveAsJpegAsync(
        string path,
        int quality = 85,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveAsJpeg(path, quality);
        return Task.CompletedTask;
    }

    /// <summary>Clears all managed pixel samples and releases this image.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        Array.Clear(pixels);
        disposed = true;
    }

    internal SKBitmap ToSkiaBitmap()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var bitmap = new SKBitmap(
            new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul)
        );
        var bytes = CopyRgbaBytes();
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bitmap.GetPixels(), bytes.Length);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    internal static RgbImage FromSkiaBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var result = new RgbImage(bitmap.Width, bitmap.Height);
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var value = bitmap.GetPixel(x, y);
            result.pixels[y * result.Width + x] = new(
                value.Red,
                value.Green,
                value.Blue,
                value.Alpha
            );
        }
        return result;
    }

    private RgbImage Render(int width, int height, Action<SKCanvas, SKImage> operation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ValidateDimensions(width, height);
        using var source = ToSkiaBitmap();
        using var image = SKImage.FromBitmap(source);
        using var target = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)
        );
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            operation(canvas, image);
            canvas.Flush();
        }
        using var transformed = FromSkiaBitmap(target);
        var copy = transformed.pixels.ToArray();
        Replace(copy, width, height);
        return this;
    }

    private void Replace(RgbaPixel[] replacement, int width, int height)
    {
        Array.Clear(pixels);
        pixels = replacement;
        Width = width;
        Height = height;
    }

    private void ValidatePosition(int x, int y)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if ((uint)x >= (uint)Width)
            throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(y));
    }

    internal static void ValidateDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaximumPixels)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Image dimensions must be positive and contain at most 64 megapixels."
            );
    }

    private void Encode(Stream stream, SKEncodedImageFormat format, int quality)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (quality < 1 || quality > 100)
            throw new ArgumentOutOfRangeException(nameof(quality));
        using var bitmap = ToSkiaBitmap();
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded =
            image.Encode(format, quality)
            ?? throw new InvalidOperationException("Image encoding failed.");
        encoded.SaveTo(stream);
    }
}
