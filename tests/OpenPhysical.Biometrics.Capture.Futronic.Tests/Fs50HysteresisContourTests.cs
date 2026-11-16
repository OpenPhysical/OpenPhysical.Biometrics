// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50HysteresisContourTests
{
    [Test]
    public void DarkCoreDoesNotHideAttachedFaintRidgesFromTheFinalContactContour()
    {
        var frame = Fs50EvidenceFrameFixture.Create(HysteresisFingerFixture.Annulus(), 1);
        var observed = Fs50ContactAnalysis.Analyze(frame).Value;
        observed.Present.Should().BeTrue();
        var contour = Fs50ContactContourRegion
            .Create(
                frame,
                observed.Diagnostics,
                Fs50ContactContourPolicy.Default,
                CancellationToken.None
            )
            .Value;
        _ = HysteresisContourExport.Write(frame, contour);
        foreach (var (x, y) in new[] { (264, 350), (536, 350), (400, 166), (400, 534) })
            contour
                .FeatureMask[y * 800 + x]
                .Should()
                .Be(1, "coherent faint ridges belong to the same contact");
        contour.FeatureMask[350 * 800 + 220].Should().Be(0);
        contour.ContactMask[350 * 800 + 400].Should().Be(1);
    }

    [Test]
    public void WeakRidgeBridgeCannotMergeTwoStrongFingerContactsIntoOne()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var observed = rig.Observe(HysteresisFingerFixture.WeakBridge());
        observed.Contact.ComponentCount.Should().BeGreaterThan(1);
        observed.Proof.Qualified.Should().BeFalse();
    }
}
