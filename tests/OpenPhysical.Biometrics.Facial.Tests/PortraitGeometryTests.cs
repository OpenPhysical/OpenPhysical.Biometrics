// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class PortraitGeometryTests
{
    private static Result<PortraitCropPlan, PortraitFinding> Crop(
        PortraitFace face,
        ReviewedPortraitGeometry review,
        int width,
        int height
    ) => PortraitCrop.Calculate(face, face.Features.Value, review, width, height);

    [Test]
    public void ReviewedGeometryProducesUniformSourceSupportedCrop()
    {
        var (face, review) = TestFixtures.Geometry();
        var result = Crop(face, review, 1200, 1600);
        result.Should().Succeed();
        var plan = result.Value;
        using (new AssertionScope())
        {
            plan.Measurements.HeadWidth.Should().BeApproximately(256, .001);
            plan.Measurements.EyeHeightFraction.Should().BeApproximately(.4, .001);
            plan.Measurements.HeadHeight.Should().BeLessThanOrEqualTo(512);
            plan.Measurements.ReviewedHeadBoundary.Should().BeTrue();
        }
        var point = new PortraitPoint(532, 731);
        var restored = plan.Transform.Inverse(plan.Transform.Map(point));
        restored.Value.X.Should().BeApproximately(point.X, 1e-8);
        restored.Value.Y.Should().BeApproximately(point.Y, 1e-8);
    }

    [Test]
    public void SmallHeadsAndImpossibleSourceCoverageRequireReview()
    {
        var (face, review) = TestFixtures.Geometry();
        Crop(face, review with { ViewerRightEar = new PortraitPoint(630, 650) }, 1200, 1600)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("HeadGeometry");
        Crop(face, review, 650, 900).Should().Fail().Which.Code.Should().Be("SourceCoverage");
    }

    [Test]
    public void RollCorrectionPreservesTheReviewedMeasurements()
    {
        var (face, review) = TestFixtures.Geometry();
        var rotate = new PortraitTransform(Math.Cos(.15), Math.Sin(.15), 40, 100);
        Maybe<PortraitPoint> Map(Maybe<PortraitPoint> p) => p.Select(rotate.Map);
        var tilted = face with
        {
            Landmarks = face
                .Landmarks.Select(p =>
                {
                    var q = rotate.Map(new(p.X, p.Y));
                    return new FaceMeshPoint(q.X, q.Y, p.Z);
                })
                .ToImmutableArray(),
            ViewerLeftEar = rotate.Map(face.ViewerLeftEar),
            ViewerRightEar = rotate.Map(face.ViewerRightEar),
        };
        var result = Crop(
            tilted,
            review with
            {
                Crown = Map(review.Crown),
                ViewerLeftEar = Map(review.ViewerLeftEar),
                ViewerRightEar = Map(review.ViewerRightEar),
            },
            1600,
            1800
        );
        result.Should().Succeed();
        result.Value.Measurements.HeadWidth.Should().BeApproximately(256, .001);
    }

    [Test]
    public void IndependentGeometryRejectsOffCenterFeaturesAndExcessiveHeadHeight()
    {
        var (face, review) = TestFixtures.Geometry();
        var points = face.Landmarks.ToArray();
        points[13] = points[13] with { X = 800 };
        points[14] = points[14] with { X = 800 };
        Crop(face with { Landmarks = points.ToImmutableArray() }, review, 1200, 1600)
            .Should()
            .Fail();
        Crop(face, review with { Crown = new PortraitPoint(600, 0) }, 1200, 1600).Should().Fail();
        var result = Crop(face, review with { ViewerLeftEar = default }, 1200, 1600);
        result.Should().Succeed();
        var measurements = result.Value.Measurements;
        measurements.CrownOrigin.Should().Be(PortraitMeasurementOrigin.Reviewed);
        measurements.ViewerLeftEarOrigin.Should().Be(PortraitMeasurementOrigin.Estimated);
        measurements.ChinOrigin.Should().Be(PortraitMeasurementOrigin.Mesh);
    }
}
