// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Piv.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class PortraitGeometryProfileTests
{
    [TestCase(420, false)]
    [TestCase(421, true)]
    public void ImageWidthMustExceedTheProfileMinimum(int width, bool supported)
    {
        var result = PivPortraitGeometry.Assess(width, 640, 240, 400, .4);
        result.Should().Succeed();
        result.Value.ImageDimensionsSatisfied.Should().Be(supported);
        result.Value.IsSatisfied.Should().Be(supported);
    }

    [TestCase(240, 512, .3)]
    [TestCase(256, 512, .5)]
    public void MandatoryGeometryIncludesTheBoundary(
        double headWidth,
        double headHeight,
        double eyes
    )
    {
        var result = PivPortraitGeometry.Assess(448, 640, headWidth, headHeight, eyes);
        result.Should().Succeed();
        result.Value.IsSatisfied.Should().BeTrue();
    }

    [Test]
    public void MeasurementsIdentifyTheFailedLimitWithoutConflatingOtherChecks()
    {
        var result = PivPortraitGeometry.Assess(448, 640, 256.01, 512.01, .5001).Value;
        result.HeadResolutionSatisfied.Should().BeTrue();
        result.HeadWidthRatioSatisfied.Should().BeFalse();
        result.HeadHeightSatisfied.Should().BeFalse();
        result.EyePositionSatisfied.Should().BeFalse();
        PivPortraitGeometry
            .Assess(480, 640, 239.99, 400, .2999)
            .Value.HeadResolutionSatisfied.Should()
            .BeFalse();
    }

    [Test]
    public void AdvisoryFramingRecommendationsAreNotMandatoryFailures()
    {
        PivPortraitGeometry.Assess(960, 640, 240, 100, .4).Value.IsSatisfied.Should().BeTrue();
    }

    [TestCase(double.NaN, 400, .4)]
    [TestCase(256, double.PositiveInfinity, .4)]
    [TestCase(256, 400, double.NaN)]
    [TestCase(0, 400, .4)]
    public void InvalidMeasurementsCannotProduceAnAssessment(
        double width,
        double height,
        double eyes
    )
    {
        PivPortraitGeometry
            .Assess(480, 640, width, height, eyes)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PivErrorCodes.InvalidPortraitMeasurements);
    }
}
