// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>Selects a motion-ranked quality frontier using explicit operator policy.</summary>
public static class CaptureQualitySelection
{
    internal const string Version = "nfiq-pareto-motion-v1";

    /// <summary>Chooses a usable baseline and a meaningfully improved non-dominated alternative.</summary>
    /// <param name="candidates">Measured quality and independent acquisition summaries.</param>
    /// <param name="policy">Minimum independent gains, without combining score scales.</param>
    /// <returns>Selected and baseline indices, or an explicit absence of assessable candidates.</returns>
    public static Result<CaptureQualityChoice, DesktopError> Select(
        ImmutableArray<CaptureCandidateSummary> candidates,
        FingerprintQualitySelectionPolicy policy
    )
    {
        var valid = CaptureSelectionInput.Check(candidates);
        if (valid.IsFailure)
            return valid.Error;
        ImmutableArray<int> usable = candidates.IsDefault
            ? []
            : Enumerable
                .Range(0, candidates.Length)
                .Where(index => candidates[index].Quality.IsAssessed)
                .ToImmutableArray();
        if (usable.IsEmpty)
            return new DesktopError(
                "Capture.NoAssessableCandidates",
                "None of the frozen images could be assessed. Try another capture."
            );
        var baseline = Rank(usable, candidates).First();
        var alternatives = usable
            .Where(index =>
                index == baseline || Improves(candidates[index], candidates[baseline], policy)
            )
            .ToImmutableArray();
        var frontier = alternatives.Where(index =>
            !alternatives.Any(other =>
                other != index && Dominates(candidates[other], candidates[index])
            )
        );
        var selected = Rank(frontier, candidates).First();
        return new CaptureQualityChoice(
            selected,
            baseline,
            selected == baseline
                ? CaptureSelectionReason.MotionBaseline
                : CaptureSelectionReason.QualityImprovement
        );
    }

    private static IOrderedEnumerable<int> Rank(
        IEnumerable<int> indices,
        ImmutableArray<CaptureCandidateSummary> candidates
    ) =>
        indices
            .OrderBy(index => candidates[index].Motion.GetValueOrDefault(double.PositiveInfinity))
            .ThenBy(index =>
                candidates[index].ClippedFraction.GetValueOrDefault(double.PositiveInfinity)
            )
            .ThenByDescending(index => candidates[index].ReliableCoverage)
            .ThenByDescending(index =>
                candidates[index].ObservedAt.GetValueOrDefault(long.MinValue)
            );

    private static bool Improves(
        CaptureCandidateSummary candidate,
        CaptureCandidateSummary baseline,
        FingerprintQualitySelectionPolicy policy
    ) =>
        Dominates(candidate, baseline)
        && (
            baseline.Quality.Nfiq1.Nfiq1 - candidate.Quality.Nfiq1.Nfiq1 >= policy.MinimumNfiq1Gain
            || candidate.Quality.Nfiq2.Value.Score - baseline.Quality.Nfiq2.Value.Score
                >= policy.MinimumNfiq2Gain
        );

    private static bool Dominates(
        CaptureCandidateSummary candidate,
        CaptureCandidateSummary other
    ) =>
        candidate.Quality.Nfiq1.Nfiq1 <= other.Quality.Nfiq1.Nfiq1
        && candidate.Quality.Nfiq2.Value.Score >= other.Quality.Nfiq2.Value.Score
        && (
            candidate.Quality.Nfiq1.Nfiq1 < other.Quality.Nfiq1.Nfiq1
            || candidate.Quality.Nfiq2.Value.Score > other.Quality.Nfiq2.Value.Score
        );
}
