using System.Drawing;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Checks owned image memory, real native transforms, and lossless interchange.</summary>
[TestFixture]
public sealed class RgbImageTests
{
    /// <summary>Rejects TIFF alpha conventions that would change the meaning of stored RGB samples.</summary>
    /// <param name="extraSample">The TIFF ExtraSamples value, or null when the tag is absent.</param>
    [TestCase(null)]
    [TestCase((ushort)0)]
    [TestCase((ushort)1)]
    public void TiffRequiresExplicitStraightAlpha(ushort? extraSample) =>
        Assert.Throws<NotSupportedException>(() => RgbImage.Load(RgbaTiff(extraSample)));

    /// <summary>Preserves straight RGBA samples when TIFF explicitly identifies unassociated alpha.</summary>
    [Test]
    public void TiffStraightAlphaPreservesSamples()
    {
        using var image = RgbImage.Load(RgbaTiff(2));
        Assert.That(image[0, 0], Is.EqualTo(new RgbaPixel(60, 30, 15, 128)));
    }

    /// <summary>Copies input pixels and keeps cloned images independent.</summary>
    [Test]
    public void InputAndClonesHaveIndependentStorage()
    {
        byte[] input = [255, 0, 0, 0, 0, 255];
        using var image = RgbImage.FromRgbBytes(input, 2, 1);
        using var clone = image.Clone();
        Array.Clear(input);
        image[0, 0] = new(0, 255, 0);
        Assert.Multiple(() =>
        {
            Assert.That(clone[0, 0].R, Is.EqualTo(255));
            Assert.That(image[0, 0].G, Is.EqualTo(255));
            Assert.That(clone.CopyRgbBytes(), Is.EqualTo(new byte[] { 255, 0, 0, 0, 0, 255 }));
        });
    }

    /// <summary>Clears retained managed spans and releases native pixel storage on disposal.</summary>
    [Test]
    public void DisposalClearsManagedRowsAndRejectsFurtherOperations()
    {
        var image = new RgbImage(2, 1, Color.White);
        var row = image.GetRowSpan(0);
        image.Dispose();
        image.Dispose();
        Assert.That(row[0].R, Is.Zero);
        Assert.That(row[0].A, Is.Zero);
        Assert.Throws<ObjectDisposedException>(() => image.Clone());
    }

    /// <summary>Preserves RGB pixels in both lossless formats used by the compatibility API.</summary>
    /// <param name="tiff">True selects baseline TIFF; false selects PNG.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void LosslessEncodingRoundTripsPixelSamples(bool tiff)
    {
        using var image = Pattern();
        using var stream = new MemoryStream();
        if (tiff)
            image.SaveAsTiff(stream);
        else
            image.SaveAsPng(stream);
        using var decoded = RgbImage.Load(stream.ToArray());
        Assert.Multiple(() =>
        {
            Assert.That((decoded.Width, decoded.Height), Is.EqualTo((3, 2)));
            Assert.That(decoded.CopyRgbBytes(), Is.EqualTo(image.CopyRgbBytes()));
        });
    }

    /// <summary>Produces a real JPEG stream that the decoder can read.</summary>
    [Test]
    public void JpegEncodingUsesTheNativeCodec()
    {
        using var image = new RgbImage(16, 16, Color.Red);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, 90);
        var bytes = stream.ToArray();
        using var decoded = RgbImage.Load(bytes);
        Assert.Multiple(() =>
        {
            Assert.That(bytes[..2], Is.EqualTo(new byte[] { 0xFF, 0xD8 }));
            Assert.That((decoded.Width, decoded.Height), Is.EqualTo((16, 16)));
            Assert.That(decoded[8, 8].R, Is.GreaterThan(240));
        });
    }

    /// <summary>Preserves sample values when cropping and flipping pixel positions.</summary>
    [Test]
    public void CropAndReflectionPreservePixelCoordinates()
    {
        using var image = Pattern();
        var expected = image[2, 1];
        image.Crop(new Rectangle(1, 0, 2, 2)).FlipHorizontal();
        Assert.That((image.Width, image.Height), Is.EqualTo((2, 2)));
        Assert.That(image[0, 1], Is.EqualTo(expected));
    }

    /// <summary>Expands a quarter-turn canvas to the exact transposed dimensions.</summary>
    [Test]
    public void QuarterTurnUsesExactExpandedCanvasDimensions()
    {
        using var image = Pattern();
        image.Rotate(90);
        Assert.That((image.Width, image.Height), Is.EqualTo((2, 3)));
    }

    /// <summary>Exercises native resize, centred padding, and compositing.</summary>
    [Test]
    public void ResizeAndPadKeepTheRequestedGeometry()
    {
        using var image = new RgbImage(8, 4, Color.Red);
        image.Resize(4, 4, fillCrop: true).Pad(8, 8).BackgroundColor(Color.White);
        Assert.Multiple(() =>
        {
            Assert.That((image.Width, image.Height), Is.EqualTo((8, 8)));
            Assert.That(image[0, 0].G, Is.EqualTo(255));
            Assert.That(image[4, 4].R, Is.EqualTo(255));
            Assert.That(image[4, 4].G, Is.Zero);
        });
    }

    /// <summary>Rejects invalid geometry before allocating an image.</summary>
    /// <param name="width">Invalid input width.</param>
    /// <param name="height">Invalid input height.</param>
    [TestCase(0, 10)]
    [TestCase(-1, 10)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public void InvalidGeometryIsRejectedBeforeAllocation(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new RgbImage(width, height));

    /// <summary>Rejects truncated image data and malformed TIFF directory offsets.</summary>
    [Test]
    public void MalformedEncodedInputsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => RgbImage.Load(ReadOnlySpan<byte>.Empty));
        Assert.Throws<ArgumentException>(() => RgbImage.Load(new byte[] { 1, 2, 3 }));
        Assert.Throws<ArgumentException>(() =>
            RgbImage.Load(new byte[] { 0x49, 0x49, 0x2A, 0, 0xFF, 0xFF, 0xFF, 0x7F })
        );
    }

    /// <summary>Checks baseline resolution fields directly in the TIFF directory.</summary>
    [Test]
    public void TiffWritesResolutionAndSortedBaselineTags()
    {
        using var image = Pattern();
        using var stream = new MemoryStream();
        image.SaveAsTiff(stream);
        stream.Position = 4;
        using var reader = new BinaryReader(stream);
        stream.Position = reader.ReadUInt32();
        var count = reader.ReadUInt16();
        var entries = Enumerable
            .Range(0, count)
            .Select(_ =>
                (
                    Tag: reader.ReadUInt16(),
                    Type: reader.ReadUInt16(),
                    Count: reader.ReadUInt32(),
                    Value: reader.ReadUInt32()
                )
            )
            .ToArray();
        Assert.That(entries.Select(entry => entry.Tag), Is.Ordered);
        Assert.That(
            entries.Select(entry => entry.Tag),
            Is.SupersetOf(new ushort[] { 282, 283, 296 })
        );
        foreach (var tag in new ushort[] { 282, 283 })
        {
            var entry = entries.Single(entry => entry.Tag == tag);
            Assert.That((entry.Type, entry.Count), Is.EqualTo(((ushort)5, 1u)));
            stream.Position = entry.Value;
            Assert.That((reader.ReadUInt32(), reader.ReadUInt32()), Is.EqualTo((72u, 1u)));
        }
        var unit = entries.Single(entry => entry.Tag == 296);
        Assert.That((unit.Type, unit.Count, unit.Value), Is.EqualTo(((ushort)3, 1u, 2u)));
    }

    private static byte[] RgbaTiff(ushort? extraSample)
    {
        var count = extraSample.HasValue ? 12 : 11;
        var bitsOffset = 8 + 2 + count * 12 + 4;
        var pixelOffset = bitsOffset + 8;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[] { 0x49, 0x49, 42, 0 });
        writer.Write(8u);
        writer.Write((ushort)count);
        Entry(256, 4, 1, 1);
        Entry(257, 4, 1, 1);
        Entry(258, 3, 4, (uint)bitsOffset);
        Entry(259, 3, 1, 1);
        Entry(262, 3, 1, 2);
        Entry(273, 4, 1, (uint)pixelOffset);
        Entry(274, 3, 1, 1);
        Entry(277, 3, 1, 4);
        Entry(278, 4, 1, 1);
        Entry(279, 4, 1, 4);
        Entry(284, 3, 1, 1);
        if (extraSample.HasValue)
            Entry(338, 3, 1, extraSample.Value);
        writer.Write(0u);
        for (var channel = 0; channel < 4; channel++)
            writer.Write((ushort)8);
        writer.Write(new byte[] { 60, 30, 15, 128 });
        return stream.ToArray();

        void Entry(ushort tag, ushort type, uint values, uint value)
        {
            writer.Write(tag);
            writer.Write(type);
            writer.Write(values);
            writer.Write(value);
        }
    }

    private static RgbImage Pattern()
    {
        var image = new RgbImage(3, 2);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
            image[x, y] = new((byte)(x * 80), (byte)(y * 100), (byte)(x + y));
        return image;
    }
}
