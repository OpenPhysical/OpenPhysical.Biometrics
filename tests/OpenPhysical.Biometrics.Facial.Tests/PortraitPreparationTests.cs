// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using SkiaSharp;

internal sealed class PortraitPreparationTests
{
    [Test]
    public void PortraitDetectionOwnsItsDataAndRepeatedCallsAgree()
    {
        var source = TestFixtures.Bytes("glenn.jpg");
        var original = source.ToArray();
        using var service = TestFixtures.NewPreparer();
        var first = service.Prepare(source);
        first.Should().Succeed();
        first.Value.Faces.Should().HaveCount(1);
        first.Value.Faces[0].Landmarks.Should().HaveCount(478);
        Parallel.For(
            0,
            4,
            _ =>
                service
                    .Prepare(source)
                    .Value.Faces[0]
                    .Landmarks.Should()
                    .Equal(first.Value.Faces[0].Landmarks)
        );
        source.Should().Equal(original);
    }

    [Test]
    public void BlankAndMalformedInputsHaveExplicitOutcomes()
    {
        using var service = TestFixtures.NewPreparer();
        var png = PortraitEncoding.EncodePng(new byte[480 * 640 * 3], 480, 640).Value;
        var blank = service.Prepare(png);
        blank.Should().Succeed();
        blank.Value.Status.Should().Be(PortraitStatus.NoFace);
        service.Prepare([1, 2, 3]).Should().Fail();
    }

    [Test]
    public void NoiseIsRejectedAndMultipleFacesRequireSelection()
    {
        using var service = TestFixtures.NewPreparer();
        var noise = new byte[480 * 640 * 3];
        new Random(1976).NextBytes(noise);
        service
            .Prepare(PortraitEncoding.EncodePng(noise, 480, 640).Value)
            .Value.Status.Should()
            .Be(PortraitStatus.NoFace);
        var encoded = Pair(TestFixtures.PathOf("bush.jpg"));
        var result = service.Prepare(encoded);
        result.Should().Succeed();
        result.Value.Status.Should().Be(PortraitStatus.FaceSelectionRequired);
        result.Value.Faces.Should().HaveCount(2);
        result
            .Value.Faces[0]
            .Features.Value.NoseBridge.X.Should()
            .BeLessThan(result.Value.Faces[1].Features.Value.NoseBridge.X);
        service
            .Prepare(encoded, 1)
            .Value.Status.Should()
            .NotBe(PortraitStatus.FaceSelectionRequired);
        service
            .Prepare(encoded, 2)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.FaceIndexOutOfRange);
    }

    [Test]
    public void CatControlProducesNoFace()
    {
        using var service = TestFixtures.NewPreparer();
        var result = service.Prepare(TestFixtures.Bytes("maddie.jpeg"));
        result.Should().Succeed();
        result.Value.Status.Should().Be(PortraitStatus.NoFace);
    }

    private static byte[] Pair(string path)
    {
        using var portrait = SKBitmap.Decode(path);
        using var pair = new SKBitmap(portrait.Width * 2, portrait.Height);
        using (var canvas = new SKCanvas(pair))
        {
            canvas.DrawBitmap(portrait, 0, 0);
            canvas.DrawBitmap(portrait, portrait.Width, 0);
        }
        using var image = SKImage.FromBitmap(pair);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
