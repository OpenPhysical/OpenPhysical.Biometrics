// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50WholeContactObservedMetricsTests
{
    [TestCase(false, Fs50WholeContactDecision.QualifiedReference)]
    [TestCase(true, Fs50WholeContactDecision.QualifiedReference)]
    [TestCase(false, Fs50WholeContactDecision.ExposureReference)]
    [TestCase(true, Fs50WholeContactDecision.ExposureReference)]
    public void QualificationCannotHideARejectedContactOrWholeObservedRegion(
        bool rejectObserved,
        Fs50WholeContactDecision decision
    )
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var pixels = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(pixels, 280, 210, 240, 320);
        var observed = rig.Observe(pixels);
        observed.Proof.Qualified.Should().BeTrue();
        var record = observed.Proof.Record;
        var contradictory = record with
        {
            Decision = decision,
            Metrics = rejectObserved ? record.Metrics : Rejected(record.Metrics),
            ObservedMetrics = rejectObserved
                ? Rejected(record.ObservedMetrics)
                : record.ObservedMetrics,
        };
        var contour = Fs50ContactContourRegion
            .Create(
                observed.Frame,
                observed.Contact.Diagnostics,
                Fs50ContactContourPolicy.Default,
                CancellationToken.None
            )
            .Value;
        contradictory
            .Validate(contour)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidFrame);
    }

    [Test]
    public void RecordedRidgeExtentIsDiagnosticRatherThanASpatialAdmissionThreshold()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        var pixels = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(pixels, 280, 210, 240, 320);
        var observed = rig.Observe(pixels);
        observed.Proof.Qualified.Should().BeTrue();
        var recorded = observed.Proof.Record with
        {
            RidgeEvidence = observed.Proof.Record.RidgeEvidence with
            {
                RidgeExteriorArea = 4000,
                LargestRidgeExteriorArea = 4000,
            },
        };
        var contour = Fs50ContactContourRegion
            .Create(
                observed.Frame,
                observed.Contact.Diagnostics,
                Fs50ContactContourPolicy.Default,
                CancellationToken.None
            )
            .Value;
        recorded
            .Validate(contour, observed.Contact.Diagnostics.ObservedContactMask.AsSpan())
            .Should()
            .Succeed();
    }

    [Test]
    public void AStaticWeakTailOutsideSelectedContactStillRejectsTheWholeObservation()
    {
        using var rig = new WholeContactRig();
        var baseline = Fs50EvidenceFrameFixture.Empty();
        _ = WeakContactFixture.Paint(baseline, 650, 170, 16, 400);
        rig.Learn(baseline).Should().BeTrue();
        var pixels = baseline.ToArray();
        _ = WholeContactRig.Ridges(pixels, 480, 200, 64, 96);
        _ = WeakContactFixture.Paint(pixels, 544, 210, 106, 16);
        var observed = rig.Observe(pixels);
        observed.Contact.ComponentCount.Should().Be(1);
        var record = observed.Proof.Record;
        record.Metrics.LargestUncorroboratedExteriorArea.Should().BeLessThan(4000);
        record
            .ObservedMetrics.LargestUncorroboratedExteriorArea.Should()
            .BeGreaterThanOrEqualTo(4000);
        record.ObservedMetrics.StaticExteriorArea.Should().BeGreaterThanOrEqualTo(4000);
        record.Decision.Should().Be(Fs50WholeContactDecision.StaticExterior);
        observed.Proof.Qualified.Should().BeFalse();
        observed.Proof.ExposureReady.Should().BeFalse();
    }

    private static Fs50WholeContactMetrics Rejected(Fs50WholeContactMetrics source) =>
        new(source.ContactArea, source.ContactArea - 4000, 4000, 4000, 0, 4000);
}
