// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Buffers.Binary;
using System.IO.Compression;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

internal sealed class PortraitSegmentationTests
{
    [TestCase("bush")]
    [TestCase("glenn")]
    public void ConvertedNetworkAgreesPixelwiseWithOriginalGoogleTflite(string name)
    {
        using var service = new PortraitSegmenter(NullLogger<PortraitSegmenter>.Instance);
        var image = PortraitImage.Decode(TestFixtures.Bytes($"selfie-{name}-input.png")).Value;
        var segmented = service.Segment(image);
        segmented.Should().Succeed();
        var expected = Reference(name);
        var actual = segmented.Value.Confidence;
        actual.Length.Should().Be(expected.Length);
        actual.Select((value, i) => Math.Abs(value - expected[i])).Max().Should().BeLessThan(1e-4f);
        actual.Should().OnlyContain(value => float.IsFinite(value) && value >= 0 && value <= 1);
    }

    [Test]
    public void MaskRetainsItsSourceIdentityAndPixelCenterMappingAfterServiceDisposal()
    {
        var image = PortraitImage.Decode(TestFixtures.Bytes("bush.jpg")).Value;
        var service = new PortraitSegmenter(NullLogger<PortraitSegmenter>.Instance);
        var result = service.Segment(image);
        result.Should().Succeed();
        var mask = result.Value;
        service.Dispose();
        service.Segment(image).Should().Fail().Which.Code.Should().Be(FacialErrorCodes.Disposed);
        mask.SourceSha256.Should().Be(image.SourceSha256);
        mask.SourceWidth.Should().Be(image.Width);
        mask.SourceHeight.Should().Be(image.Height);
        var point = new PortraitPoint(101d * image.Width / 256 - .5, 81d * image.Height / 256 - .5);
        var expected =
            (
                mask.Confidence[80 * 256 + 100]
                + mask.Confidence[80 * 256 + 101]
                + mask.Confidence[81 * 256 + 100]
                + mask.Confidence[81 * 256 + 101]
            ) / 4;
        mask.Sample(point).Should().Succeed().Which.Should().BeApproximately(expected, 1e-6f);
        mask.Sample(new(0, 0)).Value.Should().Be(mask.Confidence[0]);
        mask.Sample(new(image.Width - 1, image.Height - 1)).Value.Should().Be(mask.Confidence[^1]);
        foreach (
            var invalid in new[]
            {
                new PortraitPoint(-1, 0),
                new(double.NaN, 0),
                new(image.Width, 0),
                new(0, image.Height),
            }
        )
            mask.Sample(invalid)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(FacialErrorCodes.MaskSampleOutsideSource);
    }

    [TestCase((byte)0)]
    [TestCase((byte)255)]
    public void UniformEmptyFramesDoNotBecomeForeground(byte level)
    {
        using var service = new PortraitSegmenter(NullLogger<PortraitSegmenter>.Instance);
        var image = PortraitImage
            .FromSrgb(Enumerable.Repeat(level, 256 * 256 * 3).ToArray(), 256, 256)
            .Value;
        service.Segment(image).Value.Confidence.Max().Should().BeLessThan(1e-5f);
    }

    private static float[] Reference(string name)
    {
        using var input = new MemoryStream(TestFixtures.Bytes($"selfie-{name}-mask.f32.gz"));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        var bytes = output.ToArray();
        return Enumerable
            .Range(0, bytes.Length / 4)
            .Select(index => BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(index * 4, 4)))
            .ToArray();
    }
}
