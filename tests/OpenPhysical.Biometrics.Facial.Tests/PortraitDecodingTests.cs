// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using SkiaSharp;

internal sealed class PortraitDecodingTests
{
    // A minimal APP1 segment whose only tag is the EXIF orientation.
    private static byte[] WithOrientation(byte[] jpeg, int orientation)
    {
        var exif = Convert.FromHexString(
            "ffe100224578696600004949"
                + "2a000800000001001201030001000000"
                + orientation.ToString("x2")
                + "00000000000000"
        );
        return jpeg.Take(2).Concat(exif).Concat(jpeg.Skip(2)).ToArray();
    }

    private static byte[] GradientJpeg()
    {
        using var bitmap = new SKBitmap(8, 5);
        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 8; x++)
            bitmap.SetPixel(x, y, new SKColor((byte)(x * 30), (byte)(y * 50), 80));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        return data.ToArray();
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void ExifOrientationDefinesTheReviewedCoordinateSpace(int orientation)
    {
        var jpeg = GradientJpeg();
        var plain = PortraitDecoder.Decode(jpeg).Value;
        var decoded = PortraitDecoder.Decode(WithOrientation(jpeg, orientation));
        decoded.Should().Succeed();
        var actual = decoded.Value;
        actual.Width.Should().Be(orientation >= 5 ? 5 : 8);
        actual.Height.Should().Be(orientation >= 5 ? 8 : 5);
        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 8; x++)
        {
            var (dx, dy) = orientation switch
            {
                2 => (7 - x, y),
                3 => (7 - x, 4 - y),
                4 => (x, 4 - y),
                5 => (y, x),
                6 => (4 - y, x),
                7 => (4 - y, 7 - x),
                8 => (y, 7 - x),
                _ => (x, y),
            };
            for (var c = 0; c < 3; c++)
                actual
                    .Rgb[(dy * actual.Width + dx) * 3 + c]
                    .Should()
                    .Be(plain.Rgb[(y * 8 + x) * 3 + c]);
        }
    }

    [Test]
    public void EmbeddedLinearColorProfileIsConvertedToSrgb()
    {
        using var linear = SKColorSpace.CreateSrgbLinear();
        using var bitmap = new SKBitmap(
            new SKImageInfo(8, 8, SKColorType.Rgba8888, SKAlphaType.Opaque, linear)
        );
        var samples = bitmap.GetPixelSpan();
        for (var i = 0; i < 64; i++)
        {
            samples[i * 4] = samples[i * 4 + 1] = samples[i * 4 + 2] = 128;
            samples[i * 4 + 3] = 255;
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var decoded = PortraitDecoder.Decode(encoded.ToArray());
        decoded.Should().Succeed();
        // Half-intensity linear light maps to about 188 in 8-bit sRGB.
        decoded.Value.Rgb[0].Should().BeInRange(186, 190);
    }

    [Test]
    public void EmptyAndMalformedBytesFailWithStableCodes()
    {
        PortraitDecoder
            .Decode([])
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.EncodedImageSize);
        PortraitDecoder
            .Decode([1, 2, 3])
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.UnsupportedImage);
    }

    [Test]
    public void TransparentPixelsAreRejected()
    {
        using var bitmap = new SKBitmap(
            new SKImageInfo(4, 4, SKColorType.Rgba8888, SKAlphaType.Unpremul)
        );
        bitmap.Erase(new SKColor(10, 20, 30, 100));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        PortraitDecoder
            .Decode(encoded.ToArray())
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.ImageNotOpaque);
    }
}
