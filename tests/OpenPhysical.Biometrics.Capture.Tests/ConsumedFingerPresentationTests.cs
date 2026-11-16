// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ConsumedFingerPresentationTests
{
    [Test]
    public void StableChangedContactRearmsAfterAcceptanceWithoutInventingAnEmptyObservation()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var mask = Enumerable.Repeat((byte)1, 16).ToImmutableArray();
        var first = Frame(false);
        gate.Observe(first, mask, 1);
        clock.Advance(1000);
        gate.Observe(first, mask, 1).Value.ShouldCapture.Should().BeTrue();
        gate.Reset(requireRemoval: true);
        for (var index = 0; index < 3; index++)
        {
            clock.Advance(100);
            var held = gate.Observe(Frame(false, true), mask, 1).Value;
            held.RequiresRemoval.Should().BeTrue();
            held.ShouldCapture.Should().BeFalse();
        }
        var replacement = Frame(true);
        gate.Observe(replacement, mask, 1).Value.RequiresRemoval.Should().BeTrue();
        clock.Advance(100);
        var changed = gate.Observe(replacement, mask, 1).Value;
        changed.RequiresRemoval.Should().BeFalse();
        changed.Present.Should().BeTrue();
        changed.ShouldCapture.Should().BeFalse();
        clock.Advance(1000);
        gate.Observe(replacement, mask, 1).Value.ShouldCapture.Should().BeTrue();
    }

    private static CaptureFrame Frame(bool horizontal, bool lighter = false) =>
        PresenceFrameFixture.Create(
            Enumerable
                .Range(0, 16)
                .Select(index =>
                    (horizontal ? index / 4 : index % 4) < 2
                        ? (lighter ? (byte)80 : (byte)40)
                        : (lighter ? (byte)160 : (byte)200)
                )
                .ToArray(),
            4,
            4,
            lighter ? 220 : 200
        );
}
