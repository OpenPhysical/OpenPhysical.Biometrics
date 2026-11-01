// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

// Express both axes in the policy's normalized target units. The nearest
// point is either the target itself or its projection onto a polygon edge.
internal static class PortraitTarget
{
    internal static (double Scale, double EyeY) Nearest(FeasibleRegion region, double headWidth)
    {
        var normalized = region
            .Vertices.Select(p => (X: (p.Scale * headWidth - 256) / 16, Y: (p.Eye - 256) / 64))
            .ToArray();
        var best = Contains(normalized) ? (X: 0.0, Y: 0.0) : Closest(normalized);
        return ((best.X * 16 + 256) / headWidth, best.Y * 64 + 256);
    }

    private static bool Contains((double X, double Y)[] polygon)
    {
        var twiceArea = polygon
            .Select(
                (p, i) =>
                    p.X * polygon[(i + 1) % polygon.Length].Y
                    - p.Y * polygon[(i + 1) % polygon.Length].X
            )
            .Sum();
        return twiceArea > 1e-12
            && polygon
                .Select(
                    (start, i) =>
                    {
                        var end = polygon[(i + 1) % polygon.Length];
                        return (end.X - start.X) * -start.Y - (end.Y - start.Y) * -start.X;
                    }
                )
                .All(cross => !(cross < -1e-10));
    }

    private static (double X, double Y) Closest((double X, double Y)[] polygon)
    {
        var best = polygon[0];
        var bestCost = double.PositiveInfinity;
        for (var i = 0; i < polygon.Length; i++)
        {
            var start = polygon[i];
            var end = polygon[(i + 1) % polygon.Length];
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var length = dx * dx + dy * dy;
            var fraction =
                length > 0 ? Math.Clamp(-(start.X * dx + start.Y * dy) / length, 0, 1) : 0;
            var candidate = (X: start.X + fraction * dx, Y: start.Y + fraction * dy);
            var cost = candidate.X * candidate.X + candidate.Y * candidate.Y;
            if (cost < bestCost)
            {
                best = candidate;
                bestCost = cost;
            }
        }
        return best;
    }
}
