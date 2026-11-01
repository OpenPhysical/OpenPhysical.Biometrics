// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;

namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Builds deterministic images for tests, failing the test if construction fails.</summary>
internal static class TestImages
{
    /// <summary>Creates a 3x2 image whose pixels encode their coordinates.</summary>
    /// <returns>An image owned by the caller.</returns>
    internal static RgbImage Pattern()
    {
        var image = RgbImage.Create(3, 2).Value;
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
            image.SetPixel(x, y, new((byte)(x * 80), (byte)(y * 100), (byte)(x + y)));
        return image;
    }

    /// <summary>Creates a single-colour image.</summary>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <param name="color">Fill colour.</param>
    /// <returns>An image owned by the caller.</returns>
    internal static RgbImage Solid(int width, int height, Color color) =>
        RgbImage.Create(width, height, color).Value;

    /// <summary>Reads a pixel that is known to exist.</summary>
    /// <param name="image">Image to read.</param>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <returns>The pixel value.</returns>
    internal static RgbaPixel At(this RgbImage image, int x, int y) => image.GetPixel(x, y).Value;

    /// <summary>Encodes an image to PNG bytes.</summary>
    /// <param name="image">Image to encode.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] Png(RgbImage image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
