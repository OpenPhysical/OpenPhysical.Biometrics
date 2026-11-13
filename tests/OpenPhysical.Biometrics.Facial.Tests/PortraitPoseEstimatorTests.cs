// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Collections.Immutable;
using System.Text.Json;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class PortraitPoseEstimatorTests
{
    [Test]
    public void PerspectiveProjectionFixturesRecoverKnownPhysicalRotationsAtBothImageAspects()
    {
        foreach (var fixture in References())
        {
            var result = PortraitPoseEstimator.Estimate(
                fixture.Landmarks,
                fixture.Width,
                fixture.Height
            );
            result.Should().Succeed();
            result.Value.PitchDegrees.Should().BeApproximately(fixture.Pitch, .001);
            result.Value.YawDegrees.Should().BeApproximately(fixture.Yaw, .001);
            result.Value.RollDegrees.Should().BeApproximately(fixture.Roll, .001);
        }
    }

    [Test]
    public void ValidPhysicalPoseIsNotRejectedByAnApparentEyeLineTiltOverFiveDegrees()
    {
        var fixture = References().Single(p => p.Yaw == -4.9);
        var points = fixture.Landmarks;
        var pose = PortraitPoseEstimator.Estimate(points, fixture.Width, fixture.Height).Value;
        var face = new PortraitFace(0, 1, 1, points, default, default) { Pose = pose };
        var features = face.Features.Value;
        var dx = features.ViewerRightEye.X - features.ViewerLeftEye.X;
        var dy = features.ViewerRightEye.Y - features.ViewerLeftEye.Y;
        Math.Abs(Math.Atan2(dy, dx) * 180 / Math.PI).Should().BeGreaterThan(5);
        PortraitSourcePose.Validate(face, features).Should().Succeed();
    }

    [Test]
    public void CompactOrNonfiniteLandmarksCannotProduceAFrontalPose()
    {
        var compact = Enumerable.Repeat(new FaceMeshPoint(100, 100, 0), 478).ToImmutableArray();
        PortraitPoseEstimator
            .Estimate(compact, 1200, 1600)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.PoseUnassessable);
        var invalid = compact.SetItem(10, new FaceMeshPoint(double.NaN, 100, 0));
        PortraitPoseEstimator
            .Estimate(invalid, 1200, 1600)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.PoseUnassessable);
    }

    [TestCase("bush.jpg", true)]
    [TestCase("glenn.jpg", false)]
    public void SourcePoseIsCachedAndRejectsTheRotatedPublicPortrait(string name, bool frontal)
    {
        using var service = TestFixtures.NewPreparer();
        var image = PortraitImage.Decode(TestFixtures.Bytes(name)).Value;
        var analysis = service.Analyze(image).Value;
        var face = analysis.Faces.Single();
        face.Pose.HasValue.Should().BeTrue();
        var pose = face.Pose.Value;
        var withinLimit =
            Math.Abs(pose.PitchDegrees) < 5
            && Math.Abs(pose.YawDegrees) < 5
            && Math.Abs(pose.RollDegrees) < 5;
        withinLimit.Should().Be(frontal);
        var result = service.Prepare(analysis);
        result.Should().Succeed();
        result.Value.Faces.Single().Pose.Value.Should().BeSameAs(pose);
        result
            .Value.Findings.Any(f =>
                f.Code is "SourcePitch" or "SourceYaw" or "SourceRoll" or "SourcePoseUnassessable"
            )
            .Should()
            .Be(!frontal);
        if (!frontal)
        {
            result.Value.Status.Should().Be(PortraitStatus.ReviewRequired);
            var metadata = new FacialAcquisitionMetadata(DateTimeOffset.UtcNow, 0, 6, true, true);
            TestFixtures
                .NewRecordPreparer()
                .Prepare(result.Value, metadata)
                .Value.Record.HasNoValue.Should()
                .BeTrue();
        }
    }

    private static ImmutableArray<PortraitPoseReference> References() =>
        JsonSerializer.Deserialize<ImmutableArray<PortraitPoseReference>>(
            TestFixtures.Bytes("pose-projections.json")
        );
}
