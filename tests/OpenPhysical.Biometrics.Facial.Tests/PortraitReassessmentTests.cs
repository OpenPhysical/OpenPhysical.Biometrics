// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class PortraitReassessmentTests
{
    [Test]
    public void ToleranceChangesReusePixelsAndCannotClearUnrelatedFailures()
    {
        var portrait = TestFixtures.PreparedPortrait().Portrait;
        portrait.Findings.Should().Contain(f => f.Code == "MouthOpen");
        var relaxed = PortraitCaptureChecks.Reassess(
            portrait,
            PortraitMouthPolicy.Create(.2).Value
        );
        relaxed.Should().Succeed();
        relaxed.Value.Status.Should().Be(PortraitStatus.Prepared);
        relaxed.Value.RgbPixels.Equals(portrait.RgbPixels).Should().BeTrue();
        relaxed.Value.Transform.Value.Should().BeSameAs(portrait.Transform.Value);
        relaxed.Value.SourceSha256.Should().Be(portrait.SourceSha256);
        relaxed.Value.MouthMeasurement.Value.Should().BeSameAs(portrait.MouthMeasurement.Value);
        var turned = portrait with
        {
            Findings =
            [
                .. portrait.Findings,
                new PortraitFinding("SourceYaw", "Face the camera.")
                {
                    Action = PortraitFindingAction.Retake,
                },
            ],
        };
        var stillRejected = PortraitCaptureChecks.Reassess(
            turned,
            PortraitMouthPolicy.Create(.2).Value
        );
        stillRejected.Should().Succeed();
        stillRejected.Value.Status.Should().Be(PortraitStatus.ReviewRequired);
        stillRejected.Value.Findings.Should().ContainSingle(f => f.Code == "SourceYaw");
        stillRejected.Value.Findings.Should().NotContain(f => f.Code == "MouthOpen");
    }

    [Test]
    public void ReassessmentCannotInventAnInspectionImage()
    {
        var portrait = TestFixtures.PreparedPortrait().Portrait with { RgbPixels = [] };
        PortraitCaptureChecks
            .Reassess(portrait, PortraitMouthPolicy.Default)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.CaptureReviewRequired);
    }
}
