// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

/// <summary>One second of continuous single-finger presence using monotonic time.</summary>
[TestFixture]
public sealed class FingerPresenceGateTests
{
    private static readonly ImmutableArray<byte> Mask = ImmutableArray.Create<byte>(1, 1, 1, 1);

    /// <summary>999 ms is too early and 1000 ms triggers once, regardless of pixel movement.</summary>
    [Test]
    public void FirstSingleFingerFrameStartsADelayInsteadOfCapturingImmediately()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var first = gate.Observe(Frame(), Mask, 1).Value;
        first.Present.Should().BeTrue();
        first.SingleFinger.Should().BeTrue();
        first.ShouldCapture.Should().BeFalse();
        clock.Advance(999);
        gate.Observe(Frame(150), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1);
        var completed = gate.Observe(Frame(200), Mask, 1).Value;
        completed.ShouldCapture.Should().BeTrue();
        completed.Remaining.Should().Be(TimeSpan.Zero);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeFalse();
    }

    /// <summary>Multiple fingers restart the continuous single-finger interval.</summary>
    [Test]
    public void MultipleFingersRestartTheContinuousSingleFingerInterval()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(999);
        var multiple = gate.Observe(Frame(), Mask, 2).Value;
        multiple.Present.Should().BeTrue();
        multiple.SingleFinger.Should().BeFalse();
        multiple.ShouldCapture.Should().BeFalse();
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(999);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeTrue();
    }

    /// <summary>Absence resets both the timer and a consumed presentation.</summary>
    [Test]
    public void RemovalAndReturnRequireOneNewSecond()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        gate.Observe(Frame(), Mask, 1);
        clock.Advance(1000);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeTrue();
        var absent = gate.Observe(Frame(), [], 0).Value;
        absent.Present.Should().BeFalse();
        absent.ShouldCapture.Should().BeFalse();
        gate.Observe(Frame(), [], 0).Value.PresentationCleared.Should().BeTrue();
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(999);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeTrue();
    }

    /// <summary>Explicit retry starts one new second with a held finger; advancement requires lift.</summary>
    [Test]
    public void ResetCanRetryAHeldFingerOrRequireItsRemoval()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        gate.Observe(Frame(), Mask, 1);
        clock.Advance(1000);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeTrue();
        gate.Reset(requireRemoval: true).Should().Succeed();
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1000);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        gate.Reset(requireRemoval: false).Should().Succeed();
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1000);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeTrue();
    }

    /// <summary>Invalid evidence never consumes a presentation or contributes elapsed presence.</summary>
    [Test]
    public void InvalidObservationFailsWithoutConsumingThePresentation()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        gate.Observe(Frame(), Mask, 1);
        clock.Advance(999);
        gate.Observe(Frame(), ImmutableArray.Create((byte)1), 1)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
        gate.Observe(Frame(), Mask, -1)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
        gate.Observe(Frame(), Mask, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
        gate.Observe(Frame(), [], 1)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1000);
        gate.Observe(Frame(), Mask, 1).Value.ShouldCapture.Should().BeTrue();
    }

    private static CaptureFrame Frame(byte value = 100) =>
        PresenceFrameFixture.Create([value, value, value, value]);
}
