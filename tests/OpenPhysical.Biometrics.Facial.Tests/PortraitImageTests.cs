// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class PortraitImageTests
{
    [Test]
    public void CameraRgbInputIsOwnedAndItsIdentityIncludesThePixelGeometry()
    {
        byte[] pixels = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110];
        var first = PortraitImage.FromSrgb(pixels, 2, 2);
        var reshaped = PortraitImage.FromSrgb(pixels, 4, 1);
        first.Should().Succeed();
        reshaped.Should().Succeed();
        first.Value.SourceSha256.Should().NotBe(reshaped.Value.SourceSha256);
        pixels[0] = 255;
        first.Value.RgbPixels[0].Should().Be(0);
        first.Value.Width.Should().Be(2);
        first.Value.Height.Should().Be(2);
    }

    [TestCase(0, 2, 12)]
    [TestCase(2, -1, 12)]
    [TestCase(2, 2, 11)]
    [TestCase(int.MaxValue, int.MaxValue, 12)]
    public void InvalidCameraBuffersFailBeforeProcessing(int width, int height, int length) =>
        PortraitImage
            .FromSrgb(new byte[length], width, height)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.InvalidPixelBuffer);

    [Test]
    public void EncodedAndCameraPixelWorkflowsProduceTheSameFaceGeometry()
    {
        var decoded = PortraitImage.Decode(TestFixtures.Bytes("glenn.jpg")).Value;
        var raw = PortraitImage
            .FromSrgb(decoded.RgbPixels.AsSpan(), decoded.Width, decoded.Height)
            .Value;
        using var service = TestFixtures.NewPreparer();
        var encoded = service.Prepare(decoded);
        var camera = service.Prepare(raw);
        encoded.Should().Succeed();
        camera.Should().Succeed();
        camera.Value.SourceSha256.Should().Be(raw.SourceSha256);
        encoded.Value.SourceSha256.Should().Be(decoded.SourceSha256);
        camera
            .Value.Faces.Single()
            .Landmarks.Should()
            .Equal(encoded.Value.Faces.Single().Landmarks);
        camera.Value.RgbPixels.Should().Equal(encoded.Value.RgbPixels);
    }

    [Test]
    public void OversizedEncodedInputIsRejectedBeforeDecode()
    {
        PortraitImage
            .Decode(new byte[64 * 1024 * 1024 + 1])
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.EncodedImageSize);
    }
}
