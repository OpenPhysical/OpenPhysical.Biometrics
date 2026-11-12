// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50WholeContactAnchorTests
{
    [Test]
    public void GradualMotionCannotMoveTheOriginalQualifiedAnchorAcrossTheSensor()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var original = FingerAt(280);
        rig.Observe(original).Proof.Qualified.Should().BeTrue();
        var first = rig.Observe(FingerAt(288), 220).Proof;
        first.Qualified.Should().BeTrue();
        foreach (var x in new[] { 296, 304, 312, 320 })
        {
            var proof = rig.Observe(FingerAt(x), 220).Proof;
            proof.Qualified.Should().BeTrue();
            proof
                .Record.QualifiedAnchorDigest.Value.Should()
                .Be(first.Record.QualifiedAnchorDigest.Value);
        }
        rig.Observe(FingerAt(360), 220).Proof.Qualified.Should().BeFalse();
    }

    [Test]
    public void AcquisitionGapDiscardsTheReferenceAnchorBeforeSpatialAdmission()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var pixels = FingerAt(280);
        rig.Observe(pixels).Proof.Qualified.Should().BeTrue();
        rig.Clock.Advance(1001);
        var afterGap = rig.Observe(pixels, 220).Proof;
        afterGap.Record.Decision.Should().Be(Fs50WholeContactDecision.SpatialCapture);
        afterGap.Record.QualifiedAnchorDigest.HasValue.Should().BeFalse();
        afterGap.Record.ReferenceDigest.HasNoValue.Should().BeTrue();
        afterGap.Record.Metrics.CorroboratedArea.Should().Be(0);
        afterGap
            .Record.ObservedMetrics.UnobservedExteriorArea.Should()
            .Be(afterGap.Record.ObservedMetrics.ContactArea);
        rig.Observe(pixels)
            .Proof.Record.Decision.Should()
            .Be(Fs50WholeContactDecision.QualifiedReference);
    }

    [Test]
    public void CenteredCoherentPressureGrowthSurvivesAnExposureChange()
    {
        using var rig = new WholeContactRig();
        rig.Learn(Fs50EvidenceFrameFixture.Empty(), 220).Should().BeTrue();
        var pixels = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(pixels, 280, 210, 240, 320);
        var first = rig.Observe(pixels, 220);
        first.Proof.Qualified.Should().BeTrue();
        var wider = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(wider, 268, 210, 264, 320);
        var observed = rig.Observe(wider, 236);
        var geometry = first.Frame.Provenance.OutputGeometry;
        var before = CaptureRegionGeometry
            .Create(geometry, first.Contact.Diagnostics.ObservedContactMask)
            .Value;
        var after = CaptureRegionGeometry
            .Create(geometry, observed.Contact.Diagnostics.ObservedContactMask)
            .Value;
        (after.Area - before.Area).Should().BeGreaterThan(4000);
        after.IntersectionOverUnion(before).Value.Should().BeGreaterThan(0.5);
        after.DisplacementFrom(before).Value.Should().BeLessThan(before.Diagonal * 0.2);
        observed.Proof.Record.Decision.Should().Be(Fs50WholeContactDecision.QualifiedGeometry);
        observed.Proof.Record.ReferenceDigest.HasNoValue.Should().BeTrue();
        observed.Proof.Qualified.Should().BeTrue();
        observed.Proof.ExposureReady.Should().BeTrue();
    }

    [Test]
    public void ASubstantialNewAppendageCannotInheritExposureFallbackTrust()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        rig.Observe(FingerAt(280)).Proof.Qualified.Should().BeTrue();
        var enlarged = FingerAt(280);
        _ = WholeContactRig.Ridges(enlarged, 440, 280, 160, 96);
        var observed = rig.Observe(enlarged, 220);
        observed.Contact.ComponentCount.Should().Be(1);
        observed.Proof.Qualified.Should().BeFalse();
        observed.Proof.Record.Decision.Should().Be(Fs50WholeContactDecision.UnresolvedExterior);
        observed.Proof.Record.Metrics.UnobservedExteriorArea.Should().BeGreaterThanOrEqualTo(4000);
    }

    [Test]
    public void ANewWeakBranchOutsideSelectedContactCannotBorrowItsExposureAnchor()
    {
        var defaults = Fs50WholeContactPolicy.Default;
        var evidence = Fs50EvidencePolicy
            .Create(defaults.Evidence.Options with { RidgeBoundaryTolerance = 48 })
            .Value;
        using var rig = new WholeContactRig(
            Fs50WholeContactPolicy
                .Create(defaults.Options, evidence, defaults.EmptySensor, defaults.Presentation)
                .Value
        );
        rig.Learn().Should().BeTrue();
        var original = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(original, 280, 210, 240, 320);
        var first = rig.Observe(original);
        first.Proof.Qualified.Should().BeTrue();
        var pixels = original.ToArray();
        _ = WeakContactFixture.Paint(pixels, 548, 220, 16, 300);
        _ = WeakContactFixture.Paint(pixels, 520, 360, 28, 16);
        var observed = rig.Observe(pixels, 220);
        observed.Contact.SpatialComponentCount.Should().Be(1);
        observed.Contact.Diagnostics.LargestRidgeExteriorArea.Should().BeLessThan(4000);
        observed.Proof.Record.Metrics.LargestUncorroboratedExteriorArea.Should().BeLessThan(4000);
        observed
            .Proof.Record.ObservedMetrics.UnobservedExteriorArea.Should()
            .BeGreaterThanOrEqualTo(4000);
        observed.Proof.Record.Decision.Should().Be(Fs50WholeContactDecision.UnresolvedExterior);
        observed.Proof.Qualified.Should().BeFalse();
        observed.Proof.ExposureReady.Should().BeFalse();
        rig.Clock.Advance(1001);
        var afterGap = rig.Observe(pixels, 236).Proof;
        afterGap.Record.Decision.Should().Be(Fs50WholeContactDecision.Unavailable);
        afterGap.Qualified.Should().BeFalse();
    }

    private static byte[] FingerAt(int x)
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(pixels, x, 210, 160, 240);
        return pixels;
    }
}
