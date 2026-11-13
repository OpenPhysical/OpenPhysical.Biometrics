// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Buffers.Binary;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Imaging;

internal sealed class PortraitJpeg2000Tests
{
    [Test]
    public void EncodedColorPortraitDecodesWithoutModelsAndPreservesOrientationAndChannels()
    {
        var pixels = new byte[32 * 32 * 3];
        for (var y = 0; y < 32; y++)
        for (var x = 0; x < 32; x++)
        {
            var index = (y * 32 + x) * 3;
            pixels[index] = (byte)(x * 8);
            pixels[index + 1] = (byte)(y * 8);
            pixels[index + 2] = (byte)(((x + y) % 32) * 8);
        }
        var encoded = PortraitEncoding.EncodeJpeg2000(pixels, 32, 32);
        encoded.Should().Succeed();
        var decoded = PortraitEncoding.DecodeJpeg2000(encoded.Value);
        decoded.Should().Succeed();
        using var image = decoded.Value;
        image.Width.Should().Be(32);
        image.Height.Should().Be(32);
        var actual = image.CopyRgbBytes().Value;
        actual
            .Select((sample, index) => Math.Abs(sample - pixels[index]))
            .Average()
            .Should()
            .BeLessThan(30);
        image.GetPixel(24, 4).Value.R.Should().BeGreaterThan(image.GetPixel(24, 4).Value.G);
        image.GetPixel(4, 24).Value.G.Should().BeGreaterThan(image.GetPixel(4, 24).Value.R);
    }

    [Test]
    public void MalformedTruncatedAndExcessiveGeometryAreRejectedBeforePixelDecoding()
    {
        PortraitEncoding
            .DecodeJpeg2000(ReadOnlyMemory<byte>.Empty)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.EncodedImageSize);
        PortraitEncoding
            .DecodeJpeg2000("not an image"u8.ToArray())
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.UnsupportedImage);
        var pixels = new byte[32 * 32 * 3];
        new Random(42).NextBytes(pixels);
        var encoded = PortraitEncoding.EncodeJpeg2000(pixels, 32, 32).Value;
        PortraitEncoding
            .DecodeJpeg2000(encoded.AsMemory(0, encoded.Length - 4))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.UnsupportedImage);
        var oversized = (byte[])encoded.Clone();
        var siz = oversized.AsSpan().IndexOf(new byte[] { 255, 79, 255, 81 });
        siz.Should().BeGreaterThanOrEqualTo(0);
        BinaryPrimitives.WriteUInt32BigEndian(oversized.AsSpan(siz + 8, 4), 50000);
        BinaryPrimitives.WriteUInt32BigEndian(oversized.AsSpan(siz + 12, 4), 50000);
        PortraitEncoding
            .DecodeJpeg2000(oversized)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.ImageDimensionsExceeded);
    }
}
