// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Registers two minutiae constellations for visualization without changing their canonical data.</summary>
public static class FingerprintAlignment
{
    /// <summary>Finds a supported rotation, translation, and physical scale between selected views.</summary>
    /// <param name="reference">Reference record, including its physical pixel resolutions.</param>
    /// <param name="referenceViewIndex">Selected reference view in wire order.</param>
    /// <param name="probe">Probe record, including its physical pixel resolutions.</param>
    /// <param name="probeViewIndex">Selected probe view in wire order.</param>
    /// <returns>A geometric overlay, or a typed failure when support is insufficient. This is independent of biometric identity scores.</returns>
    public static Result<FingerprintAlignmentResult, FingerprintError> Align(
        FingerprintRecord reference,
        int referenceViewIndex,
        FingerprintRecord probe,
        int probeViewIndex
    ) =>
        AlignmentInput
            .Create(reference, referenceViewIndex)
            .Bind(first =>
                AlignmentInput.Create(probe, probeViewIndex).Bind(second => Register(first, second))
            );

    private static Result<FingerprintAlignmentResult, FingerprintError> Register(
        AlignmentInput reference,
        AlignmentInput probe
    )
    {
        var correspondence = new AlignmentCorrespondence(reference.Points, probe.Points);
        var candidates = new AlignmentCandidates(AlignmentFootprint.Create(reference.Points));
        foreach (var transform in AlignmentHypotheses.Generate(reference, probe))
        {
            var score = correspondence.Score(transform);
            _ = candidates.Consider(transform, score);
        }
        return Select(reference, probe, correspondence, candidates.Transforms);
    }

    private static Result<FingerprintAlignmentResult, FingerprintError> Select(
        AlignmentInput reference,
        AlignmentInput probe,
        AlignmentCorrespondence correspondence,
        IEnumerable<FingerprintSimilarityTransform> candidates
    )
    {
        var supported = new List<FingerprintAlignmentResult>();
        foreach (var candidate in candidates)
        {
            var refined = AlignmentRefinement.Refine(
                candidate,
                reference.Points,
                probe.Points,
                correspondence
            );
            var score = correspondence.Score(refined);
            var pairs = correspondence.Pairs();
            if (!Supported(reference, probe, pairs, score.Error))
                continue;
            supported.Add(
                AlignmentProjection.Create(reference, probe, refined, pairs, score.Error)
            );
        }
        return Choose(supported, AlignmentFootprint.Create(reference.Points));
    }

    private static Result<FingerprintAlignmentResult, FingerprintError> Choose(
        List<FingerprintAlignmentResult> supported,
        AlignmentFootprint footprint
    )
    {
        var ranked = supported
            .OrderByDescending(result => result.Pairs.Length)
            .ThenBy(result => result.RootMeanSquareError)
            .ToArray();
        if (ranked.Length == 0)
            return Failure(
                "The selected views do not have enough consistent geometric correspondences for a reference overlay."
            );
        var best = ranked[0];
        if (
            ranked
                .Skip(1)
                .Any(other =>
                    other.Pairs.Length >= Math.Ceiling(best.Pairs.Length * 0.9)
                    && other.RootMeanSquareError <= best.RootMeanSquareError + 1.5
                    && !footprint.Equivalent(best.Transform, other.Transform)
                )
        )
            return Failure(
                "Several distinct reference placements have similar support; a unique reference overlay could not be established."
            );
        return best;
    }

    private static FingerprintError Failure(string message) =>
        new(FingerprintErrorCodes.AlignmentNotEstablished, message);

    private static bool Supported(
        AlignmentInput reference,
        AlignmentInput probe,
        ImmutableArray<FingerprintAlignmentPair> pairs,
        double error
    )
    {
        var minimum = Math.Max(
            8,
            (int)Math.Ceiling(Math.Min(reference.Points.Length, probe.Points.Length) * 0.4)
        );
        if (pairs.Length < minimum || Math.Sqrt(error / pairs.Length) > 10.5)
            return false;
        var x = pairs.Average(pair => reference.Points[pair.ReferenceIndex].X);
        var y = pairs.Average(pair => reference.Points[pair.ReferenceIndex].Y);
        return pairs.Average(pair =>
                Math.Pow(reference.Points[pair.ReferenceIndex].X - x, 2)
                + Math.Pow(reference.Points[pair.ReferenceIndex].Y - y, 2)
            ) >= 1600;
    }
}
