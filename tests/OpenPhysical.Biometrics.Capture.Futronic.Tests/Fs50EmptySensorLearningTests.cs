// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50EmptySensorLearningTests
{
    [Test]
    public void LearningRequiresDistinctConfidentEmptyFramesAndTheFullMonotonicSpan()
    {
        var clock = new EmptySensorClock();
        var learning = Fs50EmptySensorLearning.Create(Fs50EmptySensorPolicy.Default, clock).Value;
        var pixels = Fs50EvidenceFrameFixture.Empty();
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;
        for (var i = 0; i < 3; i++)
        {
            learning
                .Observe(Fs50EvidenceFrameFixture.Create(pixels, i), region, "session")
                .Value.Ready.Should()
                .BeFalse();
            clock.Advance(250);
        }
        clock.Advance(-1);
        learning
            .Observe(Fs50EvidenceFrameFixture.Create(pixels, 3), region, "session")
            .Value.Ready.Should()
            .BeFalse();
        clock.Advance(1);
        var ready = learning
            .Observe(Fs50EvidenceFrameFixture.Create(pixels, 4), region, "session")
            .Value;

        ready.Ready.Should().BeTrue();
        ready.MatchingReference.Should().BeTrue();
        ready.Reference.Value.Pixels.Should().Equal(pixels);
    }

    [Test]
    public void LightContactCannotBecomeEmptyByRemainingStill()
    {
        var clock = new EmptySensorClock();
        var learning = Fs50EmptySensorLearning.Create(Fs50EmptySensorPolicy.Default, clock).Value;
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8", low: 235);
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;
        for (var i = 0; i < 8; i++)
        {
            var observed = learning
                .Observe(Fs50EvidenceFrameFixture.Create(pixels, i), region, "session")
                .Value;
            observed.Ready.Should().BeFalse();
            observed.ReferenceCount.Should().Be(0);
            clock.Advance(250);
        }
    }

    [Test]
    public void TrustedPresentationSurvivesExposureChangeWithoutAnUnmatchedComparison()
    {
        var clock = new EmptySensorClock();
        var learning = Fs50EmptySensorLearning.Create(Fs50EmptySensorPolicy.Default, clock).Value;
        _ = Learn(learning, clock, 200);
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;

        var started = learning
            .Observe(Fs50EvidenceFrameFixture.Create(pixels, 10), region, "session")
            .Value;
        var adjusted = learning
            .Observe(Fs50EvidenceFrameFixture.Create(pixels, 11, 220), region, "session")
            .Value;

        started.PresentationTrusted.Should().BeTrue();
        adjusted.Ready.Should().BeTrue();
        adjusted.PresentationTrusted.Should().BeTrue();
        adjusted.MatchingReference.Should().BeFalse();
        adjusted.Reference.HasValue.Should().BeFalse();
        adjusted.Difference.HasValue.Should().BeFalse();
    }

    [Test]
    public void SessionChangeAndExplicitClearDiscardReferencesAndPresentationTrust()
    {
        var clock = new EmptySensorClock();
        var learning = Fs50EmptySensorLearning.Create(Fs50EmptySensorPolicy.Default, clock).Value;
        _ = Learn(learning, clock, 200);
        var pixels = ContactEvidenceFixtures.PublicFinger("f0005_03.gray8");
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;

        var changed = learning
            .Observe(Fs50EvidenceFrameFixture.Create(pixels, 10), region, "new-session")
            .Value;
        changed.Ready.Should().BeFalse();
        changed.ReferenceCount.Should().Be(0);
        _ = learning.Clear();
        learning
            .Observe(Fs50EvidenceFrameFixture.Create(pixels, 11), region, "session")
            .Value.Ready.Should()
            .BeFalse();
    }

    private static bool Learn(
        Fs50EmptySensorLearning learning,
        EmptySensorClock clock,
        int exposure
    )
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        var region = Fs50ContactAnalysis.Analyze(pixels).Value;
        for (var i = 0; i < 4; i++)
        {
            _ = learning.Observe(
                Fs50EvidenceFrameFixture.Create(pixels, i, exposure),
                region,
                "session"
            );
            clock.Advance(250);
        }
        return true;
    }
}

internal sealed class EmptySensorClock : TimeProvider
{
    private long timestamp;
    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => timestamp;

    internal bool Advance(int milliseconds)
    {
        timestamp += milliseconds;
        return true;
    }
}
