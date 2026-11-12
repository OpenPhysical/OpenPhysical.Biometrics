// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class BestFramePolicyTests
{
    private static readonly ImmutableArray<byte> Mask = ImmutableArray.Create<byte>(1, 1, 1, 1);

    [Test]
    public void DeadlineSelectsABetterPreviouslyDisplayedFrameWithOriginalMetadata()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var clipped = PresenceFrameFixture.Create([0, 255, 0, 255]);
        var sharp = PresenceFrameFixture.Create([20, 180, 20, 180], exposure: 210);
        var blurred = PresenceFrameFixture.Create([100, 100, 100, 100], exposure: 220);
        gate.Observe(clipped, Mask, 1);
        clock.Advance(500);
        gate.Observe(sharp, Mask, 1);
        clock.Advance(500);
        var ready = gate.Observe(blurred, Mask, 1).Value;
        ready.ShouldCapture.Should().BeTrue();
        ready.BestFrame.HasValue.Should().BeTrue();
        ready.BestFrame.Value.Frame.Should().BeSameAs(sharp);
        ready.BestFrame.Value.Frame.Provenance.Settings.Exposure.Should().Be(210);
    }

    [Test]
    public void BufferHasAtMostFiveCandidatesAndExpiresFramesOlderThan750Milliseconds()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        gate.ConfigureDelay(TimeSpan.FromSeconds(10));
        var frame = PresenceFrameFixture.Create([20, 180, 20, 180]);
        for (var index = 0; index < 11; index++)
        {
            gate.Observe(frame, Mask, 1);
            gate.CandidateCount.Should().BeLessThanOrEqualTo(5);
            clock.Advance(100);
        }
        clock.Advance(651);
        gate.Observe(frame, Mask, 1);
        gate.CandidateCount.Should().Be(1);
    }

    [Test]
    public void LatestInvalidObservationSuppressesAnOtherwiseReadyBuffer()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var frame = PresenceFrameFixture.Create([20, 180, 20, 180]);
        gate.Observe(frame, Mask, 1);
        clock.Advance(1000);
        var invalid = gate.Observe(frame, Mask, 2).Value;
        invalid.ShouldCapture.Should().BeFalse();
        invalid.BestFrame.HasNoValue.Should().BeTrue();
        var restarted = gate.Observe(frame, Mask, 1).Value;
        restarted.ShouldCapture.Should().BeFalse();
        gate.CandidateCount.Should().Be(1);
        clock.Advance(1000);
        gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeTrue();
    }

    [Test]
    public void OneAbsentFramePreservesTheClockButTwoClearThePresentation()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var frame = PresenceFrameFixture.Create([20, 180, 20, 180]);
        gate.Observe(frame, Mask, 1);
        clock.Advance(900);
        gate.Observe(frame, [], 0).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(100);
        gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeTrue();
        gate.Observe(frame, [], 0);
        gate.Observe(frame, [], 0).Value.PresentationCleared.Should().BeTrue();
        gate.CandidateCount.Should().Be(0);
        gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeFalse();
    }

    [Test]
    public void GapGreaterThanOneSecondClearsTheOldClockAndCandidates()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var frame = PresenceFrameFixture.Create([20, 180, 20, 180]);
        gate.Observe(frame, Mask, 1);
        clock.Advance(1001);
        var resumed = gate.Observe(frame, Mask, 1).Value;
        resumed.PresentationCleared.Should().BeTrue();
        resumed.ShouldCapture.Should().BeFalse();
        gate.CandidateCount.Should().Be(1);
    }
}
