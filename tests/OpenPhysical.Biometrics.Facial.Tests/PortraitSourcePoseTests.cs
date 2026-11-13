// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class PortraitSourcePoseTests
{
    [TestCase(-25)]
    [TestCase(-5)]
    [TestCase(5)]
    [TestCase(25)]
    public void ExcessiveSourceTiltIsRejectedBeforeReviewedBoundariesCanLevelIt(double degrees)
    {
        var (face, review) = PortraitPoseTestData.Rotated(degrees);
        var result = PortraitCrop.Calculate(face, face.Features.Value, review, 1800, 2000);
        result.Should().Fail().Which.Code.Should().Be("SourceRoll");
        result.Error.Action.Should().Be(PortraitFindingAction.Retake);
    }

    [TestCase(-5, 0, 0, "SourcePitch")]
    [TestCase(5, 0, 0, "SourcePitch")]
    [TestCase(0, -5, 0, "SourceYaw")]
    [TestCase(0, 5, 0, "SourceYaw")]
    [TestCase(0, 0, -5, "SourceRoll")]
    [TestCase(0, 0, 5, "SourceRoll")]
    public void EveryEstimatedAxisRejectsTheExcludedBoundaryWithARetakeAction(
        double pitch,
        double yaw,
        double roll,
        string code
    )
    {
        var (face, review) = TestFixtures.Geometry();
        var turned = face with { Pose = new PortraitPoseEstimate(pitch, yaw, roll, 63) };
        var result = PortraitCrop.Calculate(turned, face.Features.Value, review, 1200, 1600);
        result.Should().Fail().Which.Code.Should().Be(code);
        result.Error.Action.Should().Be(PortraitFindingAction.Retake);
    }

    [TestCase(-4.9, 4.9, -4.9)]
    [TestCase(4.9, -4.9, 4.9)]
    public void EstimatedAxesInsideTheLimitDoNotRequireRetakingTheSource(
        double pitch,
        double yaw,
        double roll
    )
    {
        var (face, review) = TestFixtures.Geometry();
        var near = face with { Pose = new PortraitPoseEstimate(pitch, yaw, roll, 63) };
        PortraitCrop.Calculate(near, face.Features.Value, review, 1200, 1600).Should().Succeed();
    }

    [Test]
    public void MissingOrNonfinitePoseCannotFallBackToEyeLineTiltAlone()
    {
        var (face, review) = TestFixtures.Geometry();
        var missing = face with { Pose = default };
        var invalid = face with { Pose = new PortraitPoseEstimate(double.NaN, 0, 0, 63) };
        foreach (var source in new[] { missing, invalid })
        {
            var result = PortraitCrop.Calculate(source, face.Features.Value, review, 1200, 1600);
            result.Should().Fail().Which.Code.Should().Be("SourcePoseUnassessable");
            result.Error.Action.Should().Be(PortraitFindingAction.Retake);
        }
    }

    [TestCase(-4.9)]
    [TestCase(4.9)]
    public void SmallSourceTiltCanStillBeLeveledWithoutChangingHeadProportions(double degrees)
    {
        var (face, review) = PortraitPoseTestData.Rotated(degrees);
        var plan = PortraitCrop.Calculate(face, face.Features.Value, review, 1800, 2000);
        plan.Should().Succeed();
        var measured = plan.Value.Measurements;
        (measured.HeadHeight / measured.HeadWidth).Should().BeApproximately(550.0 / 320, .001);
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void UnusableEyeGeometryCannotBecomeAFrontalPortrait(double value)
    {
        var (face, review) = TestFixtures.Geometry();
        var features = face.Features.Value with { ViewerLeftEye = new PortraitPoint(value, 650) };
        PortraitCrop
            .Calculate(face, features, review, 1200, 1600)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("SourcePoseUnassessable");
    }

    [Test]
    public void CoincidentEyesCannotProduceASourceTiltMeasurement()
    {
        var (face, review) = TestFixtures.Geometry();
        var features = face.Features.Value;
        PortraitCrop
            .Calculate(
                face,
                features with
                {
                    ViewerRightEye = features.ViewerLeftEye,
                },
                review,
                1200,
                1600
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("SourcePoseUnassessable");
    }
}
