// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class MajorPresentationMotionTests
{
    [Test]
    public void OneMajorObservationDoesNotRestartButTwoConsecutiveObservationsDo()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var frame = PresenceFrameFixture.Create(
            Enumerable.Repeat((byte)100, 64 * 64).ToArray(),
            64,
            64
        );
        gate.Observe(frame, Patch(8), 1);
        clock.Advance(900);
        gate.Observe(frame, Patch(36), 1).Value.PresentationCleared.Should().BeFalse();
        clock.Advance(50);
        var reset = gate.Observe(frame, Patch(36), 1).Value;
        reset.PresentationCleared.Should().BeTrue();
        reset.ShouldCapture.Should().BeFalse();
        gate.CandidateCount.Should().Be(1);
        clock.Advance(50);
        gate.Observe(frame, Patch(36), 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(950);
        gate.Observe(frame, Patch(36), 1).Value.ShouldCapture.Should().BeTrue();
    }

    [Test]
    public void ReturningAfterOneMajorObservationKeepsTheOriginalDeadline()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var frame = PresenceFrameFixture.Create(
            Enumerable.Repeat((byte)100, 64 * 64).ToArray(),
            64,
            64
        );
        gate.Observe(frame, Patch(8), 1);
        clock.Advance(900);
        gate.Observe(frame, Patch(36), 1);
        clock.Advance(100);
        gate.Observe(frame, Patch(8), 1).Value.ShouldCapture.Should().BeTrue();
    }

    private static ImmutableArray<byte> Patch(int left) =>
        Enumerable
            .Range(0, 64 * 64)
            .Select(index =>
                (byte)(
                    index % 64 >= left
                    && index % 64 < left + 20
                    && index / 64 >= 20
                    && index / 64 < 40
                        ? 1
                        : 0
                )
            )
            .ToImmutableArray();
}
