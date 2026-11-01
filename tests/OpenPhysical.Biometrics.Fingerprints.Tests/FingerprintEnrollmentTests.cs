// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NFIQ1;

[TestFixture]
internal sealed class FingerprintEnrollmentTests
{
    [TestCase(1, 100)]
    [TestCase(2, 80)]
    [TestCase(3, 60)]
    [TestCase(4, 40)]
    [TestCase(5, 20)]
    public void QualityUsesOriginalNfiq1Mapping(int nfiq, int expected) =>
        FingerprintQualityAssessment
            .Create(nfiq, "test")
            .Value.PivQuality.Should()
            .Be((byte)expected);

    [Test]
    public void QualityRejectsInvalidClassificationsAndIdentities()
    {
        using (new AssertionScope())
        {
            FingerprintQualityAssessment
                .Create(0, "test")
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(FingerprintErrorCodes.InvalidQuality);
            FingerprintQualityAssessment.Create(6, "test").Should().Fail();
            FingerprintQualityAssessment.Create(1, " ").Should().Fail();
            FingerprintQualityAssessment
                .Create(1, "test", Maybe.From(1.5), FingerprintQualityStatus.Assessed)
                .Should()
                .Fail();
            FingerprintQualityAssessment
                .Create(1, "test", Maybe<double>.None, (FingerprintQualityStatus)9)
                .Should()
                .Fail();
        }
    }

    [TestCase(Nfiq1AssessmentStatus.EmptyImage, FingerprintQualityStatus.EmptyImage)]
    [TestCase(Nfiq1AssessmentStatus.TooFewMinutiae, FingerprintQualityStatus.InsufficientMinutiae)]
    public void QualityAdapterPreservesDefinedPoorImageOutcomes(
        Nfiq1AssessmentStatus status,
        FingerprintQualityStatus expected
    )
    {
        var provider = new Nfiq1FingerprintQualityProvider(new PoorImageEngine(status));
        var assessment = provider.Assess(TestCaptures.Capture(FingerPosition.RightIndex)).Value;
        using (new AssertionScope())
        {
            assessment.Nfiq1.Should().Be(5);
            assessment.PivQuality.Should().Be(20);
            assessment.Confidence.Value.Should().Be(1);
            assessment.Status.Should().Be(expected);
        }
    }

    [Test]
    public void QualityAdapterReportsUnknownStatusesAsFailures()
    {
        var provider = new Nfiq1FingerprintQualityProvider(
            new PoorImageEngine((Nfiq1AssessmentStatus)99)
        );
        provider
            .Assess(TestCaptures.Capture(FingerPosition.RightIndex))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.UnknownQualityStatus);
    }

    [Test]
    public void CandidateRankingReusesTemplatesAndPrefersIndexFingersWithinQualityClass()
    {
        var engine = new TestEngine();
        var service = new PivFingerprintEnrollmentService(engine, new TestQuality());
        var candidates = new[]
        {
            FingerPosition.LeftThumb,
            FingerPosition.LeftIndex,
            FingerPosition.RightIndex,
        }
            .Select(finger => service.Prepare(TestCaptures.Capture(finger)).Value)
            .ToArray();
        var ranked = PivFingerprintEnrollmentService.RankCandidates(
            candidates,
            preferLeftHand: true
        );
        using (new AssertionScope())
        {
            ranked
                .Value.Select(candidate => candidate.Finger)
                .Should()
                .Equal(
                    FingerPosition.LeftIndex,
                    FingerPosition.RightIndex,
                    FingerPosition.LeftThumb
                );
            engine.Extractions.Should().Be(3);
        }
    }

    [Test]
    public void CandidateRankingRejectsUnavailableFingers()
    {
        var unavailable = FingerprintEnrollmentFinger
            .Unavailable(FingerPosition.LeftIndex, FingerprintAvailability.TemporarilyUnusable)
            .Value;
        PivFingerprintEnrollmentService
            .RankCandidates([unavailable], preferLeftHand: false)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.UnavailableCandidate);
    }
}
