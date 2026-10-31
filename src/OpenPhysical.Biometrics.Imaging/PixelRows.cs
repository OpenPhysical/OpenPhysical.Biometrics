namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Provides row access during an explicitly scoped pixel operation.</summary>
public sealed class PixelRows
{
    private readonly RgbImage image;

    internal PixelRows(RgbImage image)
    {
        this.image = image;
    }

    /// <summary>Gets the number of pixels in each row.</summary>
    public int Width => image.Width;

    /// <summary>Gets the number of rows.</summary>
    public int Height => image.Height;

    /// <summary>Gets mutable pixels for one row.</summary>
    /// <param name="y">Zero-based row index.</param>
    /// <returns>A managed span belonging to the image.</returns>
    public Span<RgbaPixel> GetRowSpan(int y) => image.GetRowSpan(y);
}
