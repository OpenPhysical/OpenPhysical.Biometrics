// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class FacialCaptureOverrideTests
{
    [Test]
    public void OverrideCannotTransferToAnotherSourceOrNewFailure()
    {
        var portrait = Rejected();
        var created = FacialCaptureOverride.Create(portrait, DateTimeOffset.UnixEpoch);
        created.Should().Succeed();
        created.Value.Validate(portrait).Should().Succeed();
        created
            .Value.Validate(portrait with { SourceSha256 = new string('f', 64) })
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.InvalidCaptureOverride);
        var changed = portrait with { Findings = [.. portrait.Findings, Failure("SourceYaw")] };
        created
            .Value.Validate(portrait with { MouthPolicy = PortraitMouthPolicy.Create(.05).Value })
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.InvalidCaptureOverride);
        created
            .Value.Validate(changed)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.InvalidCaptureOverride);
    }

    [Test]
    public void OnlyExplicitUnsignedOverrideCanEncodeAndItRetainsUnknownFrontalEvidence()
    {
        var portrait = Rejected();
        var evidence = FacialCaptureOverride.Create(portrait, DateTimeOffset.UnixEpoch).Value;
        var acquisition = new FacialAcquisitionMetadata(
            DateTimeOffset.UnixEpoch,
            0,
            6,
            false,
            false
        );
        var preparer = TestFixtures.NewRecordPreparer();
        var strict = preparer.Prepare(
            portrait,
            acquisition,
            FacialPreparationPolicy.StrictPiv with
            {
                CaptureOverride = evidence,
            }
        );
        strict.Should().Succeed();
        strict.Value.Record.HasNoValue.Should().BeTrue();
        var result = preparer.Prepare(
            portrait,
            acquisition,
            FacialPreparationPolicy.UnsignedStaging with
            {
                CaptureOverride = evidence,
            }
        );
        result.Should().Succeed();
        result.Value.Status.Should().Be(FacialPreparationStatus.Prepared);
        result.Value.Record.HasValue.Should().BeTrue();
        result.Value.Acquisition.FullFrontalConfirmed.Should().BeFalse();
        result
            .Value.AcquisitionFindings.Should()
            .Contain(f => f.Code == "FullFrontalReviewUnknown");
        result.Value.CaptureOverride.Value.Should().BeSameAs(evidence);
    }

    [Test]
    public void OverrideCannotInventMissingPixelsOrBypassAnIneligibleFinding()
    {
        var portrait = Rejected();
        foreach (
            var invalid in new[]
            {
                portrait with
                {
                    RgbPixels = default,
                },
                portrait with
                {
                    Findings = default,
                },
                portrait with
                {
                    RgbPixels = [],
                },
                portrait with
                {
                    Findings =
                    [
                        new PortraitFinding("SourceCoverage", "More source image is needed."),
                    ],
                },
            }
        )
            FacialCaptureOverride
                .Create(invalid, DateTimeOffset.UnixEpoch)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(FacialErrorCodes.InvalidCaptureOverride);
    }

    private static PortraitPreparationResult Rejected() => TestFixtures.PreparedPortrait().Portrait;

    private static PortraitFinding Failure(string code) =>
        new(code, "Retake the portrait.")
        {
            Action = PortraitFindingAction.Retake,
            TestName = "Capture check",
            CanOverride = true,
        };
}
