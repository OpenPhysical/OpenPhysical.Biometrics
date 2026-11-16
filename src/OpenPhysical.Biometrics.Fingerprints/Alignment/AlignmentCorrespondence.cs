// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;

internal sealed class AlignmentCorrespondence(AlignmentPoint[] reference, AlignmentPoint[] probe)
{
    internal const double Tolerance = 14;
    private readonly Dictionary<(int X, int Y), List<int>> grid = BuildGrid(probe);
    private readonly double[] distances = new double[probe.Length];
    private readonly int[] assignments = new int[probe.Length];

    internal (int Count, double Error) Score(FingerprintSimilarityTransform transform)
    {
        Array.Fill(distances, double.PositiveInfinity);
        Array.Fill(assignments, -1);
        var rotation = Math.Atan2(transform.B, transform.A);
        for (var index = 0; index < reference.Length; index++)
            Assign(index, transform, rotation);
        var count = 0;
        var error = 0.0;
        for (var index = 0; index < probe.Length; index++)
            if (assignments[index] >= 0)
            {
                count++;
                error += distances[index];
            }
        return (count, error);
    }

    internal ImmutableArray<FingerprintAlignmentPair> Pairs() =>
        assignments
            .Select((source, target) => (source, target))
            .Where(pair => pair.source >= 0)
            .Select(pair => new FingerprintAlignmentPair(
                reference[pair.source].Index,
                probe[pair.target].Index
            ))
            .ToImmutableArray();

    internal static double DirectionError(double first, double second) =>
        Math.Abs(Math.IEEERemainder(first - second, Math.PI));

    private bool Assign(int index, FingerprintSimilarityTransform transform, double rotation)
    {
        var point = reference[index];
        var x = transform.A * point.X - transform.B * point.Y + transform.TranslationX;
        var y = transform.B * point.X + transform.A * point.Y + transform.TranslationY;
        var cellX = (int)Math.Floor(x / Tolerance);
        var cellY = (int)Math.Floor(y / Tolerance);
        var nearest = -1;
        var distance = Tolerance * Tolerance;
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
            if (grid.TryGetValue((cellX + dx, cellY + dy), out var candidates))
                foreach (var candidate in candidates)
                {
                    var target = probe[candidate];
                    var squared = (x - target.X) * (x - target.X) + (y - target.Y) * (y - target.Y);
                    if (
                        squared < distance
                        && DirectionError(point.Direction + rotation, target.Direction) <= 0.61
                    )
                    {
                        nearest = candidate;
                        distance = squared;
                    }
                }
        if (nearest < 0 || distance >= distances[nearest])
            return false;
        distances[nearest] = distance;
        assignments[nearest] = index;
        return true;
    }

    private static Dictionary<(int X, int Y), List<int>> BuildGrid(AlignmentPoint[] points)
    {
        var result = new Dictionary<(int, int), List<int>>();
        for (var index = 0; index < points.Length; index++)
        {
            var cell = (
                (int)Math.Floor(points[index].X / Tolerance),
                (int)Math.Floor(points[index].Y / Tolerance)
            );
            if (!result.TryGetValue(cell, out var members))
            {
                members = [];
                result.Add(cell, members);
            }
            members.Add(index);
        }
        return result;
    }
}
