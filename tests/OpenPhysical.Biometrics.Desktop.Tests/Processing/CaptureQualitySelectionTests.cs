// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class CaptureQualitySelectionTests
{
    [TestCase(2, 50, 1)]
    [TestCase(3, 55, 1)]
    [TestCase(3, 54, 0)]
    [TestCase(4, 99, 0)]
    [TestCase(1, 49, 0)]
    public void AnAlternativeMustBeNoWorseInEitherScoreAndMeaningfullyBetter(
        int nfiq1,
        int nfiq2,
        int selected
    )
    {
        var result = CaptureQualitySelection.Select(
            [Candidate(3, 50, 0), Candidate(nfiq1, nfiq2, 1)],
            FingerprintQualitySelectionPolicy.Default
        );
        result.Value.SelectedIndex.Should().Be(selected);
        result.Value.BaselineIndex.Should().Be(0);
    }

    [Test]
    public void DominatedCandidatesAreRemovedBeforeMotionBreaksRemainingTradeoffs()
    {
        var candidates = ImmutableArray.Create(
            Candidate(4, 40, 0),
            Candidate(3, 50, 1),
            Candidate(2, 55, 2),
            Candidate(1, 50, 3)
        );
        CaptureQualitySelection
            .Select(candidates, FingerprintQualitySelectionPolicy.Default)
            .Value.SelectedIndex.Should()
            .Be(2);
    }

    [Test]
    public void ConfiguredHysteresisIsAppliedWithoutCombiningScoreScales()
    {
        var candidates = ImmutableArray.Create(Candidate(4, 40, 0), Candidate(3, 46, 1));
        CaptureQualitySelection
            .Select(candidates, FingerprintQualitySelectionPolicy.Create(2, 7).Value)
            .Value.SelectedIndex.Should()
            .Be(0);
    }

    [Test]
    public void MotionTiesUseClippingThenReliableCoverageThenRecency()
    {
        var first = Candidate(3, 50, 0);
        var lessClipped = first with { ClippedFraction = 0.01 };
        var larger = lessClipped with { ReliableCoverage = 200 };
        var newer = larger with { ObservedAt = 10 };
        CaptureQualitySelection
            .Select([first, lessClipped, larger, newer], FingerprintQualitySelectionPolicy.Default)
            .Value.SelectedIndex.Should()
            .Be(3);
    }

    [Test]
    public void ExplicitlyUnassessableCandidatesCannotBecomeTheBaseline()
    {
        var invalid = Candidate(1, 100, 0);
        invalid = invalid with
        {
            Quality = invalid.Quality with
            {
                Nfiq2 = Maybe<FingerprintUnifiedQualityAssessment>.None,
                Diagnostics = [new("QualityImageNotAssessable", "No usable area.")],
            },
        };
        CaptureQualitySelection
            .Select([invalid, Candidate(3, 50, 1)], FingerprintQualitySelectionPolicy.Default)
            .Value.SelectedIndex.Should()
            .Be(1);
        CaptureQualitySelection
            .Select([invalid], FingerprintQualitySelectionPolicy.Default)
            .Error.Code.Should()
            .Be("Capture.NoAssessableCandidates");
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(-0.1)]
    public void InvalidMotionMetadataCannotBecomeTheBestCandidate(double motion)
    {
        var result = CaptureQualitySelection.Select(
            [Candidate(1, 80, motion)],
            FingerprintQualitySelectionPolicy.Default
        );
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Capture.InvalidSelectionInput");
    }

    [Test]
    public void OperationalQualityDiagnosticsAbortInsteadOfExcludingTheCandidate()
    {
        var invalid = Candidate(1, 80, 0);
        invalid = invalid with
        {
            Quality = invalid.Quality with
            {
                Diagnostics = [new("QualityEngineFailed", "engine failure")],
            },
        };
        var result = CaptureQualitySelection.Select(
            [invalid, Candidate(2, 60, 1)],
            FingerprintQualitySelectionPolicy.Default
        );
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("QualityEngineFailed");
    }

    private static CaptureCandidateSummary Candidate(int nfiq1, int nfiq2, double motion) =>
        new(
            "original",
            "processed",
            0,
            DateTimeOffset.UnixEpoch,
            new(
                "processed",
                CaptureImageRole.Processed,
                FingerprintQualityAssessment.Create(nfiq1, "fixture").Value,
                FingerprintUnifiedQualityAssessment.Create(nfiq2, "fixture").Value,
                []
            ),
            motion,
            0.1,
            100,
            0
        );
}
