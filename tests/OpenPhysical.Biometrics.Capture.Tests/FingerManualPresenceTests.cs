// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerManualPresenceTests
{
    [Test]
    public void ManualPresenceBeyondTheDelayDoesNotTriggerOrConsumeThePresentation()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var frame = PresenceFrameFixture.Create([64, 96, 128, 160]);
        var mask = ImmutableArray.Create<byte>(1, 1, 1, 1);
        for (var i = 0; i < 8; i++)
        {
            var observed = gate.Observe(frame, mask, 1, false).Value;

            observed.ShouldCapture.Should().BeFalse();
            observed.RequiresRemoval.Should().BeFalse();
            clock.Advance(250);
        }
    }

    [Test]
    public void ManualModeStillObservesTheRequiredConsecutiveEmptyFrames()
    {
        var gate = new FingerPresenceGate(new ControlledPresenceClock());
        var frame = PresenceFrameFixture.Create([64, 96, 128, 160]);
        _ = gate.Reset(true);

        gate.Observe(frame, [], 0, false).Value.RequiresRemoval.Should().BeTrue();
        gate.Observe(frame, [], 0, false).Value.RequiresRemoval.Should().BeFalse();
        gate.Observe(frame, ImmutableArray.Create<byte>(1, 1, 1, 1), 1, false)
            .Value.ShouldCapture.Should()
            .BeFalse();
    }
}
