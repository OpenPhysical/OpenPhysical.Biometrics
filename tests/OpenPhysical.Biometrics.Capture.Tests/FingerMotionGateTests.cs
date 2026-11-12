// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using NUnit.Framework;

/// <summary>Movement is measured against the interval's original frame rather than adjacent frames.</summary>
[TestFixture]
public sealed class FingerMotionGateTests
{
    private static readonly ImmutableArray<byte> Mask = ImmutableArray.Create<byte>(1, 1, 1, 1);

    /// <summary>Small adjacent changes accumulate until significant anchored motion restarts the delay.</summary>
    [Test]
    public void RawPixelVariationRanksCandidatesWithoutRestartingTheDeadline()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        gate.Observe(Pattern(0), Mask, 1);
        for (var index = 1; index <= 3; index++)
        {
            clock.Advance(250);
            gate.Observe(Pattern(index * 3), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        }
        clock.Advance(250);
        gate.Observe(Pattern(9), Mask, 1).Value.ShouldCapture.Should().BeTrue();
    }

    /// <summary>Exposure changes cannot reuse the old presence interval.</summary>
    [Test]
    public void AutomaticExposureChangesDoNotRestartTheDeadline()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        gate.Observe(Pattern(0), Mask, 1);
        clock.Advance(999);
        var changed = PresenceFrameFixture.Create([0, 30, 0, 30], exposure: 201);
        gate.Observe(changed, Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1);
        gate.Observe(changed, Mask, 1).Value.ShouldCapture.Should().BeTrue();
    }

    /// <summary>Geometry and foreground-mask changes cannot inherit an old interval.</summary>
    [Test]
    public void GeometryResetsButMinorMaskVariationPreservesTheNewDeadline()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        gate.Observe(Pattern(0), Mask, 1);
        clock.Advance(999);
        var frame = PresenceFrameFixture.Create(new byte[16], 4, 4);
        var full = Enumerable.Repeat((byte)1, 16).ToImmutableArray();
        gate.Observe(frame, full, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1);
        gate.Observe(frame, full, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(998);
        var changed = full.SetItem(0, 0);
        gate.Observe(frame, changed, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1);
        gate.Observe(frame, changed, 1).Value.ShouldCapture.Should().BeTrue();
    }

    /// <summary>A large contact patch can move significantly while retaining over92% mask overlap.</summary>
    [Test]
    public void MinorCentroidMovementDoesNotRestartTheDeadline()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var frame = PresenceFrameFixture.Create(
            Enumerable.Repeat((byte)100, 512 * 512).ToArray(),
            512,
            512
        );
        var first = Patch(48);
        var moved = Patch(56);
        gate.Observe(frame, first, 1);
        clock.Advance(999);
        gate.Observe(frame, moved, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1);
        gate.Observe(frame, moved, 1).Value.ShouldCapture.Should().BeTrue();
    }

    private static ImmutableArray<byte> Patch(int left) =>
        Enumerable
            .Range(0, 512 * 512)
            .Select(index =>
                (byte)(
                    index % 512 >= left
                    && index % 512 < left + 400
                    && index / 512 >= 48
                    && index / 512 < 448
                        ? 1
                        : 0
                )
            )
            .ToImmutableArray();

    private static CaptureFrame Pattern(int delta) =>
        PresenceFrameFixture.Create([
            (byte)delta,
            (byte)(30 - delta),
            (byte)delta,
            (byte)(30 - delta),
        ]);
}
