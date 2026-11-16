// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using SkiaSharp;

internal sealed class PortraitRejectedPreviewTests
{
    [Test]
    public void RejectedSourceStillHasACroppedInspectionImageButCannotCreateAStrictRecord()
    {
        using var service = TestFixtures.NewPreparer();
        var (bytes, transform) = TiltedPublicPortrait();
        var analysis = service.Analyze(PortraitImage.Decode(bytes).Value).Value;
        var review = new ReviewedPortraitGeometry(
            1,
            analysis.SourceSha256,
            1400,
            1400,
            0,
            transform.Map(new(416, 160)),
            default,
            transform.Map(new(270, 423)),
            transform.Map(new(615, 396))
        );
        var result = service.Prepare(analysis, review);
        result.Should().Succeed();
        var portrait = result.Value;
        portrait.Status.Should().Be(PortraitStatus.ReviewRequired);
        portrait.Findings.Should().Contain(f => f.Action == PortraitFindingAction.Retake);
        portrait.RgbPixels.Should().HaveCount(480 * 640 * 3);
        portrait.Transform.HasValue.Should().BeTrue();
        portrait.Measurements.Value.ReviewedHeadBoundary.Should().BeTrue();
        var metadata = new FacialAcquisitionMetadata(DateTimeOffset.UtcNow, 0, 6, true, true);
        TestFixtures
            .NewRecordPreparer()
            .Prepare(portrait, metadata)
            .Value.Record.HasNoValue.Should()
            .BeTrue();
        PortraitCrop
            .Calculate(portrait.Faces[0], portrait.Faces[0].Features.Value, review, 1400, 1400)
            .Should()
            .Fail();
    }

    [Test]
    public void InspectionFramingDoesNotRelaxSourceCoverageOrTheStrictPoseGate()
    {
        var (face, review) = TestFixtures.Geometry();
        var turned = face with { Pose = new PortraitPoseEstimate(5.2, 0, 0, 63) };
        PortraitFraming
            .Calculate(turned, turned.Features.Value, review, 1200, 1600)
            .Should()
            .Succeed();
        PortraitCrop
            .Calculate(turned, turned.Features.Value, review, 1200, 1600)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("SourcePitch");
        PortraitFraming
            .Calculate(turned, turned.Features.Value, review, 650, 900)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("SourceCoverage");
    }

    private static (byte[] Bytes, PortraitTransform Transform) TiltedPublicPortrait()
    {
        const double radians = 15 * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var transform = new PortraitTransform(
            cos,
            -sin,
            700 - cos * 404 + sin * 512,
            700 - sin * 404 - cos * 512
        );
        using var source = SKBitmap.Decode(TestFixtures.Bytes("bush.jpg"));
        using var bitmap = new SKBitmap(1400, 1400);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            canvas.Translate(700, 700);
            canvas.RotateDegrees(15);
            canvas.DrawBitmap(source, -404, -512);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return (png.ToArray(), transform);
    }
}
