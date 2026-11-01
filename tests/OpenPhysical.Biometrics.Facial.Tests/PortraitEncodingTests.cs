// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class PortraitEncodingTests
{
    [Test]
    public void NoisyPixelsEncodeToJpeg2000WithinTheCompressionLimit()
    {
        var pixels = new byte[64 * 64 * 3];
        new Random(77).NextBytes(pixels);
        var encoded = PortraitEncoding.EncodeJpeg2000(pixels, 64, 64);
        encoded.Should().Succeed();
        (encoded.Value.Length * 15L).Should().BeGreaterThanOrEqualTo(pixels.Length);
    }

    [Test]
    public void BlankPixelsExceedTheCompressionLimit()
    {
        PortraitEncoding
            .EncodeJpeg2000(new byte[480 * 640 * 3], 480, 640)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.CompressionLimit);
    }

    [Test]
    public void PngPreviewRoundTripsThroughTheDecoder()
    {
        var pixels = new byte[16 * 16 * 3];
        new Random(5).NextBytes(pixels);
        var png = PortraitEncoding.EncodePng(pixels, 16, 16);
        png.Should().Succeed();
        var decoded = PortraitDecoder.Decode(png.Value);
        decoded.Should().Succeed();
        decoded.Value.Rgb.Should().Equal(pixels);
    }

    [TestCase(0, 640, 3)]
    [TestCase(4097, 640, 3)]
    [TestCase(480, 0, 3)]
    [TestCase(480, 640, 3)]
    public void InvalidDimensionsOrBuffersFailBeforeEncoding(int width, int height, int length)
    {
        var buffer = new byte[length];
        PortraitEncoding
            .EncodeJpeg2000(buffer, width, height)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.InvalidPixelBuffer);
        PortraitEncoding
            .EncodePng(buffer, width, height)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.InvalidPixelBuffer);
    }
}
