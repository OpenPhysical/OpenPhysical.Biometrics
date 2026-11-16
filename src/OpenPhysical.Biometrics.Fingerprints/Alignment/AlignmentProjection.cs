// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;

internal static class AlignmentProjection
{
    internal static FingerprintAlignmentResult Create(
        AlignmentInput reference,
        AlignmentInput probe,
        FingerprintSimilarityTransform transform,
        ImmutableArray<FingerprintAlignmentPair> pairs,
        double error
    )
    {
        var matched = pairs.Select(pair => pair.ReferenceIndex).ToHashSet();
        var points = reference
            .Points.Select(point =>
                Project(point, reference, probe, transform, matched.Contains(point.Index))
            )
            .ToImmutableArray();
        return new(transform, pairs, Math.Sqrt(error / pairs.Length), points);
    }

    private static FingerprintAlignedMinutia Project(
        AlignmentPoint point,
        AlignmentInput reference,
        AlignmentInput probe,
        FingerprintSimilarityTransform transform,
        bool matched
    )
    {
        var x =
            (transform.A * point.X - transform.B * point.Y + transform.TranslationX)
            * probe.Record.XResolution
            / 197;
        var y =
            (transform.B * point.X + transform.A * point.Y + transform.TranslationY)
            * probe.Record.YResolution
            / 197;
        var angle = point.Direction + Math.Atan2(transform.B, transform.A);
        var degrees =
            (
                Math.Atan2(
                    -Math.Sin(angle) * probe.Record.YResolution,
                    Math.Cos(angle) * probe.Record.XResolution
                )
                    * 180
                    / Math.PI
                + 360
            ) % 360;
        var original = reference.View.Minutiae[point.Index];
        return new(x, y, degrees, original.Type, original.Quality, matched);
    }
}
