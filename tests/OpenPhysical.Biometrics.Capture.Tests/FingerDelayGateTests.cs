// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

/// <summary>Configured delay boundaries and changes use deterministic monotonic time.</summary>
[TestFixture]
public sealed class FingerDelayGateTests
{
    private static readonly ImmutableArray<byte> Mask = ImmutableArray.Create<byte>(1, 1, 1, 1);

    /// <summary>Zero captures immediately; every positive setting waits through its exact boundary.</summary>
    /// <param name="milliseconds">Validated interval.</param>
    [TestCase(0)]
    [TestCase(100)]
    [TestCase(1000)]
    [TestCase(10000)]
    public void ConfiguredDelayUsesItsExactBoundary(int milliseconds)
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        gate.ConfigureDelay(TimeSpan.FromMilliseconds(milliseconds)).Should().Succeed();
        var frame = PresenceFrameFixture.Create([10, 100, 10, 100]);
        gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().Be(milliseconds == 0);
        if (milliseconds > 0)
        {
            var remaining = milliseconds;
            while (remaining > 500)
            {
                clock.Advance(500);
                gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeFalse();
                remaining -= 500;
            }
            clock.Advance(remaining - 1);
            gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeFalse();
            clock.Advance(1);
            gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeTrue();
        }
        gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeFalse();
    }

    /// <summary>A setting change cannot reuse time accumulated under the previous delay.</summary>
    [Test]
    public void ChangingDelayStartsANewInterval()
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var frame = PresenceFrameFixture.Create([10, 100, 10, 100]);
        gate.Observe(frame, Mask, 1);
        clock.Advance(900);
        gate.ConfigureDelay(TimeSpan.FromMilliseconds(200)).Should().Succeed();
        gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(199);
        gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeFalse();
        clock.Advance(1);
        gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeTrue();
    }

    /// <summary>Invalid configuration preserves the current valid interval.</summary>
    /// <param name="milliseconds">Out-of-range delay.</param>
    [TestCase(-1)]
    [TestCase(10001)]
    public void InvalidDelayDoesNotChangeTheCurrentConfiguration(int milliseconds)
    {
        var clock = new ControlledPresenceClock();
        var gate = new FingerPresenceGate(clock);
        var frame = PresenceFrameFixture.Create([10, 100, 10, 100]);
        gate.Observe(frame, Mask, 1);
        gate.ConfigureDelay(TimeSpan.FromMilliseconds(milliseconds))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
        clock.Advance(1000);
        gate.Observe(frame, Mask, 1).Value.ShouldCapture.Should().BeTrue();
    }
}
