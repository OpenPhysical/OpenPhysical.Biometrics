// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50WholeContactGeometryReuseTests
{
    [Test]
    public void IdenticalContactGeometryStillReassessesEveryMatchedReferencePhotometricObservation()
    {
        var policy = Policy();
        using var rig = new WholeContactRig(policy);
        var empty = Fs50EvidenceFrameFixture.Empty();
        _ = WholeContactRig.Ridges(empty, 650, 210, 32, 300);
        var referenceEvidence = Fs50ContactAnalysis
            .Analyze(empty, policy.Evidence)
            .Value.Diagnostics;
        referenceEvidence.CandidateArea.Should().Be(0);
        referenceEvidence.RidgeSupportArea.Should().Be(0);
        empty
            .Average(value => (double)value)
            .Should()
            .BeGreaterThanOrEqualTo(policy.EmptySensor.Options.MinimumEmptyMeanGray);
        rig.Learn(empty).Should().BeTrue();
        var pixels = empty.ToArray();
        _ = WholeContactRig.Ridges(pixels, 480, 200, 64, 96);
        _ = WholeContactRig.Ridges(pixels, 544, 210, 106, 32);
        var unchanged = rig.Observe(pixels);
        unchanged.Proof.Qualified.Should().BeFalse();
        var changed = pixels.ToArray();
        for (var y = 210; y < 510; y++)
        for (var x = 650; x < 682; x++)
            changed[y * 800 + x] -= 4;
        var corroborated = rig.Observe(changed);
        corroborated.Contact.ContactMask.Should().Equal(unchanged.Contact.ContactMask);
        corroborated
            .Proof.Record.Identity.ContactDigest.Should()
            .Be(unchanged.Proof.Record.Identity.ContactDigest);
        corroborated
            .Proof.Record.Identity.SourceDigest.Should()
            .NotBe(unchanged.Proof.Record.Identity.SourceDigest);
        corroborated.Proof.Qualified.Should().BeTrue();
        var staticAgain = rig.Observe(pixels);
        staticAgain.Contact.ContactMask.Should().Equal(corroborated.Contact.ContactMask);
        staticAgain.Proof.Qualified.Should().BeFalse();
    }

    // The synthetic reference band fits comparison windows but stays below this test's finger width.
    private static Fs50WholeContactPolicy Policy() =>
        Fs50WholeContactPolicy
            .Create(
                new(),
                Fs50EvidencePolicy.Create(new() { MinimumWidth = 80 }).Value,
                Fs50EmptySensorPolicy.Default,
                FingerCapturePolicy.Default
            )
            .Value;
}
