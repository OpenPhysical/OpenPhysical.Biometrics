// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Checks the baseline TIFF decoder and encoder.</summary>
[TestFixture]
public sealed class BaselineTiffTests
{
    /// <summary>Rejects TIFF alpha conventions that would change the meaning of stored RGB samples.</summary>
    /// <param name="extraSample">The TIFF ExtraSamples value.</param>
    [TestCase((ushort)0)]
    [TestCase((ushort)1)]
    public void TiffRequiresExplicitStraightAlpha(ushort extraSample) =>
        Decode(TiffSpec.Rgba with { ExtraSample = extraSample })
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(ImagingErrorCodes.TiffUnsupported);

    /// <summary>Rejects RGBA TIFFs that omit the ExtraSamples tag.</summary>
    [Test]
    public void TiffWithoutExtraSamplesIsUnsupported() =>
        Decode(TiffSpec.Rgba)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(ImagingErrorCodes.TiffUnsupported);

    /// <summary>Preserves straight RGBA samples when TIFF explicitly identifies unassociated alpha.</summary>
    [Test]
    public void TiffStraightAlphaPreservesSamples()
    {
        using var image = Decode(TiffSpec.Rgba with { ExtraSample = (ushort)2 }).Value;
        image.At(0, 0).Should().Be(new RgbaPixel(60, 30, 15, 128));
    }

    /// <summary>Decodes opaque RGB and both grayscale photometric interpretations.</summary>
    [Test]
    public void TiffDecodesRgbAndGrayscale()
    {
        using var rgb = Decode(TiffSpec.Rgb).Value;
        using var whiteIsZero = Decode(TiffSpec.Gray).Value;
        using var blackIsZero = Decode(TiffSpec.Gray with { Photometric = 1 }).Value;
        using (new AssertionScope())
        {
            rgb.At(0, 0).Should().Be(new RgbaPixel(60, 30, 15, 255));
            whiteIsZero.At(0, 0).Should().Be(new RgbaPixel(55));
            blackIsZero.At(0, 0).Should().Be(new RgbaPixel(200));
        }
    }

    /// <summary>Rejects compression and colour models the decoder cannot represent.</summary>
    [Test]
    public void UnsupportedTiffFeaturesFailWithUnsupportedCode()
    {
        using (new AssertionScope())
        {
            Decode(TiffSpec.Rgb with { Compression = 5 })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.TiffUnsupported);
            Decode(TiffSpec.Rgb with { Photometric = 5 })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.TiffUnsupported);
            Decode(TiffSpec.Gray with { Photometric = 2 })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.TiffUnsupported);
        }
    }

    /// <summary>Rejects strips whose byte count disagrees with the image geometry.</summary>
    [Test]
    public void InconsistentStripLengthIsMalformed() =>
        Decode(TiffSpec.Rgb with { StripLength = 2u })
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(ImagingErrorCodes.TiffMalformed);

    /// <summary>Rejects truncated pixel data and directory offsets beyond the input.</summary>
    [Test]
    public void TruncatedTiffIsMalformed()
    {
        var bytes = TiffFixtures.Build(TiffSpec.Rgb);
        using (new AssertionScope())
        {
            RgbImageDecoder
                .Load(bytes[..^1])
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.TiffMalformed);
            RgbImageDecoder
                .Load(bytes[..6])
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.TiffMalformed);
            RgbImageDecoder
                .Load(new byte[] { 0x49, 0x49, 0x2A, 0, 0xFF, 0xFF, 0xFF, 0x7F })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.TiffMalformed);
        }
    }

    /// <summary>Rejects directories that repeat a tag.</summary>
    [Test]
    public void DuplicateTagsAreMalformed()
    {
        var bytes = TiffFixtures.Build(TiffSpec.Rgb);
        bytes[10 + 12] = bytes[10];
        bytes[10 + 13] = bytes[11];
        RgbImageDecoder
            .Load(bytes)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(ImagingErrorCodes.TiffMalformed);
    }

    /// <summary>Checks baseline resolution fields directly in the TIFF directory.</summary>
    [Test]
    public void TiffWritesResolutionAndSortedBaselineTags()
    {
        using var image = TestImages.Pattern();
        using var stream = new MemoryStream();
        image.SaveAsTiff(stream).Should().Succeed();
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
        entries.Select(entry => entry.Tag).Should().BeInAscendingOrder();
        entries.Select(entry => entry.Tag).Should().Contain(new ushort[] { 282, 283, 296 });
        foreach (var tag in new ushort[] { 282, 283 })
        {
            var entry = entries.Single(entry => entry.Tag == tag);
            (entry.Type, entry.Count).Should().Be(((ushort)5, 1u));
            stream.Position = entry.Value;
            (reader.ReadUInt32(), reader.ReadUInt32()).Should().Be((72u, 1u));
        }
        var unit = entries.Single(entry => entry.Tag == 296);
        (unit.Type, unit.Count, unit.Value).Should().Be(((ushort)3, 1u, 2u));
    }

    private static Result<RgbImage, ImagingError> Decode(TiffSpec spec) =>
        RgbImageDecoder.Load(TiffFixtures.Build(spec));
}
