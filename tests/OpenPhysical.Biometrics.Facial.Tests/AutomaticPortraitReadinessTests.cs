// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class AutomaticPortraitReadinessTests
{
    [Test]
    public void SourceSupportedAutomaticGeometryCanEncodeWithoutClaimingReviewedBoundaries()
    {
        using var service = TestFixtures.NewPreparer();
        var analysis = service
            .Analyze(PortraitImage.Decode(TestFixtures.Bytes("bush.jpg")).Value)
            .Value;
        var portrait = service
            .Prepare(analysis, default, default, PortraitMouthPolicy.Create(.15).Value)
            .Value;
        portrait.Measurements.Value.ReviewedHeadBoundary.Should().BeFalse();
        portrait.Measurements.Value.CrownOrigin.Should().Be(PortraitMeasurementOrigin.Estimated);
        FacialPortraitReadiness
            .Check(portrait, FacialPreparationPolicy.UnsignedStaging)
            .Should()
            .Succeed();
        var encoded = TestFixtures
            .NewRecordPreparer()
            .Prepare(
                portrait,
                new(DateTimeOffset.UnixEpoch, 0, 6, true, false),
                FacialPreparationPolicy.UnsignedStaging
            );
        encoded.Should().Succeed().Which.Record.HasValue.Should().BeTrue();
        portrait.Measurements.Value.ReviewedHeadBoundary.Should().BeFalse();
    }

    [TestCase("pixels")]
    [TestCase("transform")]
    [TestCase("measurements")]
    public void AutomaticReadinessStillRejectsUnusablePixelAndGeometryEvidence(string part)
    {
        var (captured, _) = TestFixtures.PreparedPortrait();
        var original = PortraitCaptureChecks
            .Reassess(captured, PortraitMouthPolicy.Create(.15).Value)
            .Value;
        FacialPortraitReadiness
            .Check(original, FacialPreparationPolicy.UnsignedStaging)
            .Should()
            .Succeed();
        var portrait = part switch
        {
            "pixels" => original with { RgbPixels = [] },
            "transform" => original with { Transform = new PortraitTransform(1, 0, double.NaN, 0) },
            _ => original with
            {
                Measurements = original.Measurements.Value with { HeadWidth = double.NaN },
            },
        };
        FacialPortraitReadiness
            .Check(portrait, FacialPreparationPolicy.UnsignedStaging)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.CaptureReviewRequired);
    }
}
