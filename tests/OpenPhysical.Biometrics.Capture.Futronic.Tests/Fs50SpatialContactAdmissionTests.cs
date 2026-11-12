// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50SpatialContactAdmissionTests
{
    [Test]
    public void AHealthyFirstContactDoesNotNeedOrInventBackgroundEvidence()
    {
        using var rig = new WholeContactRig();
        var observed = rig.Observe(Finger());
        observed.Proof.Record.Decision.Should().Be(Fs50WholeContactDecision.SpatialCapture);
        observed.Proof.Qualified.Should().BeTrue();
        observed.Proof.Record.ReferenceDigest.HasNoValue.Should().BeTrue();
        observed.Proof.Record.QualifiedAnchorDigest.HasNoValue.Should().BeTrue();
        observed.Proof.Record.Metrics.CorroboratedArea.Should().Be(0);
        observed.Proof.Record.ObservedMetrics.StaticExteriorArea.Should().Be(0);
        var changedExposure = rig.Observe(Finger(), 220);
        changedExposure.Proof.Record.Decision.Should().Be(Fs50WholeContactDecision.SpatialCapture);
        changedExposure.Proof.Record.QualifiedAnchorDigest.HasNoValue.Should().BeTrue();
    }

    [Test]
    public void LightSpatialEvidenceOnlyPermitsExposureAndCannotSeedACaptureAnchor()
    {
        using var rig = new WholeContactRig();
        var pixels = Finger();
        for (var i = 0; i < pixels.Length; i++)
            if (pixels[i] != 255)
                pixels[i] = pixels[i] == 65 ? (byte)225 : (byte)245;
        var observed = rig.Observe(pixels);
        observed.Proof.Record.Decision.Should().Be(Fs50WholeContactDecision.SpatialExposure);
        observed.Proof.Qualified.Should().BeFalse();
        observed.Proof.ExposureReady.Should().BeTrue();
        var contour = Fs50ContactContourRegion
            .Create(
                observed.Frame,
                observed.Contact.Diagnostics,
                Fs50ContactContourPolicy.Default,
                CancellationToken.None
            )
            .Value;
        observed.Proof.ValidateFor(observed.Frame, contour).Should().Fail();
        rig.Observe(Finger(), 220)
            .Proof.Record.Decision.Should()
            .Be(Fs50WholeContactDecision.SpatialCapture);
    }

    [Test]
    public void KnownMatchedContaminationCannotEscapeByChangingExposureOrWaiting()
    {
        using var rig = new WholeContactRig();
        var baseline = Fs50EvidenceFrameFixture.Empty();
        _ = WeakContactFixture.Paint(baseline, 650, 170, 16, 400);
        rig.Learn(baseline).Should().BeTrue();
        var pixels = baseline.ToArray();
        _ = WholeContactRig.Ridges(pixels, 480, 200, 64, 96);
        _ = WeakContactFixture.Paint(pixels, 544, 210, 106, 16);
        rig.Observe(pixels)
            .Proof.Record.Decision.Should()
            .Be(Fs50WholeContactDecision.StaticExterior);
        var blocked = rig.Observe(pixels, 220).Proof;
        blocked.Record.Decision.Should().Be(Fs50WholeContactDecision.Unavailable);
        blocked.Qualified.Should().BeFalse();
        blocked.ExposureReady.Should().BeFalse();
        blocked.Record.ObservedMetrics.StaticExteriorArea.Should().Be(0);
        blocked.Record.ReferenceDigest.HasNoValue.Should().BeTrue();
        rig.Clock.Advance(1001);
        rig.Observe(pixels, 220).Proof.Qualified.Should().BeFalse();
        rig.Observe(Fs50EvidenceFrameFixture.Empty(), 220);
        rig.Observe(Fs50EvidenceFrameFixture.Empty(), 220);
        rig.Observe(Finger(), 220)
            .Proof.Record.Decision.Should()
            .Be(Fs50WholeContactDecision.SpatialCapture);
    }

    [Test]
    public void ANewActualMatchedReferencePassCanRecoverWithoutFabricatingRemoval()
    {
        using var rig = new WholeContactRig();
        var baseline = Fs50EvidenceFrameFixture.Empty();
        _ = WeakContactFixture.Paint(baseline, 650, 170, 16, 400);
        rig.Learn(baseline).Should().BeTrue();
        var pixels = baseline.ToArray();
        _ = WholeContactRig.Ridges(pixels, 480, 200, 64, 96);
        _ = WeakContactFixture.Paint(pixels, 544, 210, 106, 16);
        rig.Observe(pixels).Proof.Qualified.Should().BeFalse();
        rig.Observe(Finger())
            .Proof.Record.Decision.Should()
            .Be(Fs50WholeContactDecision.QualifiedReference);
        rig.Observe(Finger(), 220)
            .Proof.Record.Decision.Should()
            .Be(Fs50WholeContactDecision.QualifiedGeometry);
    }

    private static byte[] Finger()
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(pixels, 280, 210, 240, 320);
        return pixels;
    }
}
