// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class AlignmentRefinement
{
    internal static FingerprintSimilarityTransform Refine(
        FingerprintSimilarityTransform initial,
        AlignmentPoint[] reference,
        AlignmentPoint[] probe,
        AlignmentCorrespondence correspondence
    )
    {
        var current = initial;
        for (var iteration = 0; iteration < 6; iteration++)
        {
            if (correspondence.Score(current).Count < 3)
                break;
            var fitted = Fit(correspondence.Pairs(), reference, probe);
            if (fitted.HasNoValue || fitted.Value.Scale is < 0.8 or > 1.25)
                break;
            current = fitted.Value;
        }
        return current;
    }

    private static Maybe<FingerprintSimilarityTransform> Fit(
        ImmutableArray<FingerprintAlignmentPair> pairs,
        AlignmentPoint[] reference,
        AlignmentPoint[] probe
    )
    {
        var rx = pairs.Average(pair => reference[pair.ReferenceIndex].X);
        var ry = pairs.Average(pair => reference[pair.ReferenceIndex].Y);
        var px = pairs.Average(pair => probe[pair.ProbeIndex].X);
        var py = pairs.Average(pair => probe[pair.ProbeIndex].Y);
        var denominator = 0.0;
        var numeratorA = 0.0;
        var numeratorB = 0.0;
        foreach (var pair in pairs)
        {
            var x = reference[pair.ReferenceIndex].X - rx;
            var y = reference[pair.ReferenceIndex].Y - ry;
            var u = probe[pair.ProbeIndex].X - px;
            var v = probe[pair.ProbeIndex].Y - py;
            denominator += x * x + y * y;
            numeratorA += x * u + y * v;
            numeratorB += x * v - y * u;
        }
        if (denominator < 1)
            return Maybe<FingerprintSimilarityTransform>.None;
        var a = numeratorA / denominator;
        var b = numeratorB / denominator;
        return new FingerprintSimilarityTransform(a, b, px - a * rx + b * ry, py - b * rx - a * ry);
    }
}
