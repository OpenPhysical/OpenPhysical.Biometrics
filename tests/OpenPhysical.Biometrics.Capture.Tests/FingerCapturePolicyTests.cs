// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerCapturePolicyTests
{
    [Test]
    public void DefaultsDescribeTheApprovedTolerantPresentationPolicy()
    {
        var policy = FingerCapturePolicy.Default;
        policy.CandidateLimit.Should().Be(5);
        policy.CandidateWindow.Should().Be(TimeSpan.FromMilliseconds(750));
        policy.AcquisitionGap.Should().Be(TimeSpan.FromSeconds(1));
        policy.MajorDisplacementFraction.Should().Be(0.2);
        policy.MinimumOverlap.Should().Be(0.5);
        policy.AbsenceFrames.Should().Be(2);
        policy.RepositionFrames.Should().Be(2);
    }

    [TestCase(0, 750, 0.2, 0.5)]
    [TestCase(6, 750, 0.2, 0.5)]
    [TestCase(5, 0, 0.2, 0.5)]
    [TestCase(5, 750, -0.2, 0.5)]
    [TestCase(5, 750, 0.2, 1.1)]
    public void InvalidPoliciesCannotReachTheGate(
        int limit,
        int window,
        double movement,
        double overlap
    ) =>
        FingerCapturePolicy
            .Create(
                limit,
                TimeSpan.FromMilliseconds(window),
                TimeSpan.FromSeconds(1),
                movement,
                overlap,
                2,
                2
            )
            .Should()
            .Fail();
}
