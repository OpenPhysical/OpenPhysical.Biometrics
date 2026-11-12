// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50EmptySensorLifetimeTests
{
    [Test]
    public void TheSameFrameCannotFillAReferenceByBeingObservedAgain()
    {
        var clock = new EmptySensorClock();
        var learning = Fs50EmptySensorLearning.Create(Fs50EmptySensorPolicy.Default, clock).Value;
        var pixels = Fs50EvidenceFrameFixture.Empty();
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;
        var frame = Fs50EvidenceFrameFixture.Create(pixels, 0);
        for (var i = 0; i < 8; i++)
        {
            learning.Observe(frame, region, "session").Value.ReferenceCount.Should().Be(0);
            clock.Advance(250);
        }
    }

    [Test]
    public void OnlyTheConfiguredNumberOfExposureReferencesAreRetained()
    {
        var clock = new EmptySensorClock();
        var learning = Fs50EmptySensorLearning.Create(Fs50EmptySensorPolicy.Default, clock).Value;
        var pixels = Fs50EvidenceFrameFixture.Empty();
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;
        foreach (var exposure in new[] { 64, 128, 200 })
            for (var i = 0; i < 4; i++)
            {
                var observed = learning
                    .Observe(
                        Fs50EvidenceFrameFixture.Create(pixels, exposure + i, exposure),
                        region,
                        "session"
                    )
                    .Value;
                observed.ReferenceCount.Should().BeLessThanOrEqualTo(2);
                clock.Advance(250);
            }
        var finger = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        var contact = Fs50ContactAnalysis.Analyze(finger).Value;

        var evicted = learning
            .Observe(Fs50EvidenceFrameFixture.Create(finger, 300, 64), contact, "session")
            .Value;

        evicted.MatchingReference.Should().BeFalse();
        evicted.Ready.Should().BeFalse();
        evicted.ReferenceCount.Should().Be(2);
    }

    [TestCase(0)]
    [TestCase(100)]
    [TestCase(220)]
    public void DarkUniformOcclusionIsNotConfidentEmptyGlass(int gray)
    {
        var clock = new EmptySensorClock();
        var learning = Fs50EmptySensorLearning.Create(Fs50EmptySensorPolicy.Default, clock).Value;
        var pixels = Enumerable.Repeat((byte)gray, 800 * 750).ToArray();
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;
        for (var i = 0; i < 8; i++)
        {
            learning
                .Observe(Fs50EvidenceFrameFixture.Create(pixels, i), region, "session")
                .Value.Ready.Should()
                .BeFalse();
            clock.Advance(250);
        }
    }

    [Test]
    public void ANewCalibrationCannotUsePreviousReferences()
    {
        var clock = new EmptySensorClock();
        var learning = Fs50EmptySensorLearning.Create(Fs50EmptySensorPolicy.Default, clock).Value;
        var pixels = Fs50EvidenceFrameFixture.Empty();
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;
        for (var i = 0; i < 4; i++)
        {
            _ = learning.Observe(Fs50EvidenceFrameFixture.Create(pixels, i), region, "session");
            clock.Advance(250);
        }
        var changed = learning
            .Observe(Fs50EvidenceFrameFixture.Create(pixels, 10, calibration: 1), region, "session")
            .Value;

        changed.Ready.Should().BeFalse();
        changed.ReferenceCount.Should().Be(0);
    }
}
