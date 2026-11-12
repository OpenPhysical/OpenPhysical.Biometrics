// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50WholeContactExteriorTests
{
    [Test]
    public void FarThinStaticAppendageCannotEscapeByFailingFingerWidthRequirements()
    {
        using var rig = new WholeContactRig();
        var baseline = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(baseline, 650, 210, 16, 300);
        rig.Learn(baseline).Should().BeTrue();
        var pixels = baseline.ToArray();
        _ = WholeContactRig.Ridges(pixels, 480, 200, 64, 96);
        _ = WholeContactRig.Ridges(pixels, 544, 210, 106, 16);
        var observed = rig.Observe(pixels);
        observed.Contact.Present.Should().BeTrue();
        observed.Contact.ComponentCount.Should().Be(1);
        observed.Proof.Qualified.Should().BeFalse();
        var record = observed.Proof.Record;
        record
            .Decision.Should()
            .BeOneOf(
                Fs50WholeContactDecision.StaticExterior,
                Fs50WholeContactDecision.UnresolvedExterior
            );
        Math.Max(
                record.Metrics.LargestUncorroboratedExteriorArea,
                record.ObservedMetrics.LargestUncorroboratedExteriorArea
            )
            .Should()
            .BeGreaterThanOrEqualTo(4000);
    }

    [Test]
    public void UnchangedEnclosedWeakCoreIsUncertaintyRatherThanAnAutomaticRejection()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var pixels = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(pixels, 280, 210, 240, 320);
        for (var row = 274; row < 466; row++)
            pixels.AsSpan(row * 800 + 344, 112).Fill(255);
        var observed = rig.Observe(pixels);
        observed.Proof.Qualified.Should().BeTrue();
        var contour = Fs50ContactContourRegion
            .Create(
                observed.Frame,
                observed.Contact.Diagnostics,
                Fs50ContactContourPolicy.Default,
                CancellationToken.None
            )
            .Value;
        contour.FilledHoleMask[375 * 800 + 400].Should().Be(1);
        contour.FeatureMask[375 * 800 + 400].Should().Be(1);
        contour.UncertainMask[375 * 800 + 400].Should().Be(1);
    }
}
