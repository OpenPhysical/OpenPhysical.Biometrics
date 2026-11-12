// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50WholeContactQualificationTests
{
    [Test]
    public void MatchedReferenceQualifiesThePublicFingerAndProofRemainsValidAfterClearingSessionState()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var observed = rig.Observe(ContactEvidenceFixtures.PublicFinger("f0005_03.gray8"));
        observed.Proof.Record.Decision.Should().Be(Fs50WholeContactDecision.QualifiedReference);
        observed
            .Proof.Record.ReferenceKeyDigest.Value.Should()
            .Be(observed.Proof.Record.Identity.SessionKeyDigest);
        var contour = Fs50ContactContourRegion
            .Create(
                observed.Frame,
                observed.Contact.Diagnostics,
                Fs50ContactContourPolicy.Default,
                CancellationToken.None
            )
            .Value;
        rig.Qualification.Clear().Should().Succeed();
        observed.Proof.ValidateFor(observed.Frame, contour).Should().Succeed();
        var another = Fs50EvidenceFrameFixture.Create(
            observed.Frame.Pixels.ToArray(),
            observed.Frame.Sequence + 1
        );
        observed.Proof.ValidateFor(another, contour).Should().Fail();
        observed
            .Proof.Record.ValidateSource(
                Source(observed.Frame),
                "different-driver",
                observed.Frame.Provenance.OutputGeometry
            )
            .Should()
            .Fail();
    }

    [Test]
    public void ExposureFallbackUsesTheSameQualifiedAnchorWithoutUnmatchedPhotometry()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        rig.Observe(pixels).Proof.Qualified.Should().BeTrue();
        var first = rig.Observe(pixels, 220).Proof;
        pixels[375 * 800 + 400] ^= 1;
        var second = rig.Observe(pixels, 220).Proof;
        first.Record.Decision.Should().Be(Fs50WholeContactDecision.QualifiedGeometry);
        second.Record.Decision.Should().Be(Fs50WholeContactDecision.QualifiedGeometry);
        second.Record.QualifiedAnchorDigest.HasValue.Should().BeTrue();
        second
            .Record.QualifiedAnchorDigest.Value.Should()
            .Be(first.Record.QualifiedAnchorDigest.Value);
        second.Record.ReferenceDigest.HasValue.Should().BeFalse();
        second.Record.Metrics.StaticExteriorArea.Should().Be(0);
    }

    [Test]
    public void FreshSpatialAdmissionCannotInheritOldReferenceOrAnchorEvidence()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        var missing = rig.Observe(pixels, 220).Proof;
        rig.Observe(pixels)
            .Proof.Record.Decision.Should()
            .Be(Fs50WholeContactDecision.QualifiedReference);
        var calibrated = rig.Observe(pixels, 220, calibration: 1).Proof;
        var reopened = rig.Observe(pixels, 220, session: "new-session").Proof;
        foreach (var proof in new[] { missing, calibrated, reopened })
        {
            proof.Record.Decision.Should().Be(Fs50WholeContactDecision.SpatialCapture);
            proof.Record.ReferenceDigest.HasNoValue.Should().BeTrue();
            proof.Record.ReferenceKeyDigest.HasNoValue.Should().BeTrue();
            proof.Record.QualifiedAnchorDigest.HasNoValue.Should().BeTrue();
            proof.Record.Metrics.CorroboratedArea.Should().Be(0);
            proof
                .Record.ObservedMetrics.UnobservedExteriorArea.Should()
                .Be(proof.Record.ObservedMetrics.ContactArea);
        }
    }

    [Test]
    public void RejectedLightContactIsNotRemovalButTwoConfidentEmptyFramesClearTheAnchor()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        rig.Observe(pixels).Proof.Qualified.Should().BeTrue();
        rig.Observe(ContactEvidenceFixtures.PublicFinger("f0005_03.gray8", low: 235))
            .Proof.Qualified.Should()
            .BeFalse();
        rig.Observe(pixels, 220)
            .Proof.Record.Decision.Should()
            .Be(Fs50WholeContactDecision.QualifiedGeometry);
        rig.Observe(Fs50EvidenceFrameFixture.Empty());
        rig.Observe(Fs50EvidenceFrameFixture.Empty());
        var afterRemoval = rig.Observe(pixels, 220).Proof;
        afterRemoval.Record.Decision.Should().Be(Fs50WholeContactDecision.SpatialCapture);
        afterRemoval.Record.ReferenceDigest.HasNoValue.Should().BeTrue();
        afterRemoval.Record.QualifiedAnchorDigest.HasNoValue.Should().BeTrue();
        afterRemoval.Record.Metrics.CorroboratedArea.Should().Be(0);
        afterRemoval
            .Record.ObservedMetrics.UnobservedExteriorArea.Should()
            .Be(afterRemoval.Record.ObservedMetrics.ContactArea);
    }

    private static FingerprintProcessingSource Source(CaptureFrame frame) =>
        FingerprintProcessingSource
            .Create(
                frame.Provenance.Device.DeviceId,
                frame.Sequence,
                frame.CapturedAt,
                frame.Provenance.Settings.Exposure,
                frame.Provenance.CorrectionVersion,
                Convert.ToHexStringLower(frame.Provenance.CalibrationDigest.AsSpan())
            )
            .Value;
}
