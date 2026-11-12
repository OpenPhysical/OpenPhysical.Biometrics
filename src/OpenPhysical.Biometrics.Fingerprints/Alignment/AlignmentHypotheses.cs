// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

internal static class AlignmentHypotheses
{
    internal static IEnumerable<FingerprintSimilarityTransform> Generate(
        AlignmentInput reference,
        AlignmentInput probe
    )
    {
        // Extractors can disagree on ending/bifurcation labels. Fit geometry and ridge directions.
        var first = Seeds(reference);
        var second = Seeds(probe);
        foreach (var source in first)
        foreach (var target in second)
            for (var reversal = 0; reversal < 2; reversal++)
                yield return Anchor(
                    source,
                    target,
                    target.Direction - source.Direction + reversal * Math.PI
                );
        foreach (var transform in FromPairs(first, second))
            yield return transform;
    }

    private static AlignmentPoint[] Seeds(AlignmentInput input) =>
        input
            .Points.OrderByDescending(point => input.View.Minutiae[point.Index].Quality)
            .Take(96)
            .ToArray();

    private static FingerprintSimilarityTransform Anchor(
        AlignmentPoint source,
        AlignmentPoint target,
        double radians
    )
    {
        var a = Math.Cos(radians);
        var b = Math.Sin(radians);
        return new(
            a,
            b,
            target.X - a * source.X + b * source.Y,
            target.Y - b * source.X - a * source.Y
        );
    }

    private static IEnumerable<FingerprintSimilarityTransform> FromPairs(
        AlignmentPoint[] reference,
        AlignmentPoint[] probe
    )
    {
        var second = Edges(probe);
        var remaining = 32768;
        foreach (var source in Edges(reference))
        foreach (var target in second)
        {
            var scale = target.Length / source.Length;
            if (scale is < 0.8 or > 1.25)
                continue;
            var rotation = target.Direction - source.Direction;
            if (!DirectionsAgree(source, target, rotation))
                continue;
            var a = scale * Math.Cos(rotation);
            var b = scale * Math.Sin(rotation);
            yield return new(
                a,
                b,
                target.First.X - a * source.First.X + b * source.First.Y,
                target.First.Y - b * source.First.X - a * source.First.Y
            );
            if (--remaining == 0)
                yield break;
        }
    }

    private static bool DirectionsAgree(
        AlignmentEdge source,
        AlignmentEdge target,
        double rotation
    ) =>
        AlignmentCorrespondence.DirectionError(
            source.First.Direction + rotation,
            target.First.Direction
        ) <= 0.61
        && AlignmentCorrespondence.DirectionError(
            source.Second.Direction + rotation,
            target.Second.Direction
        ) <= 0.61;

    private static AlignmentEdge[] Edges(AlignmentPoint[] points) =>
        points
            .SelectMany(first =>
                points
                    .Where(second => first.Index != second.Index)
                    .Select(second => new AlignmentEdge(first, second))
                    .Where(edge => edge.Length is >= 40 and <= 320)
                    .OrderBy(edge => edge.Length)
                    .Take(10)
            )
            .ToArray();
}
