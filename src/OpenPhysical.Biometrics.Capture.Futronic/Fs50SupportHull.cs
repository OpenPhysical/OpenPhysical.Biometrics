// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class Fs50SupportHull
{
    internal static Result<ImmutableArray<Fs50HullPoint>, CaptureError> Create(
        ReadOnlySpan<byte> support,
        CancellationToken cancellationToken
    )
    {
        var extremes = new List<Fs50HullPoint>();
        for (var y = 0; y < 750; y++)
        {
            if (cancellationToken.IsCancellationRequested)
                return Fs50MaskErrors.Cancelled;
            var row = support.Slice(y * 800, 800);
            int left = row.IndexOf((byte)1),
                right = row.LastIndexOf((byte)1);
            if (left < 0)
                continue;
            extremes.Add(new(2 * left - 1, 2 * y - 1));
            extremes.Add(new(2 * right + 1, 2 * y - 1));
            extremes.Add(new(2 * left - 1, 2 * y + 1));
            extremes.Add(new(2 * right + 1, 2 * y + 1));
        }
        if (extremes.Count == 0)
            return new CaptureError(
                Fs50ContactContourErrorCodes.NoSupport,
                "No selected contact is available from which to construct image context."
            );
        var points = extremes
            .Distinct()
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToArray();
        var lower = Chain(points);
        var upper = Chain(points.Reverse());
        return lower.Take(lower.Count - 1).Concat(upper.Take(upper.Count - 1)).ToImmutableArray();
    }

    private static List<Fs50HullPoint> Chain(IEnumerable<Fs50HullPoint> points)
    {
        var chain = new List<Fs50HullPoint>();
        foreach (var point in points)
        {
            while (chain.Count >= 2 && Cross(chain[^2], chain[^1], point) <= 0)
                chain.RemoveAt(chain.Count - 1);
            chain.Add(point);
        }
        return chain;
    }

    private static long Cross(Fs50HullPoint first, Fs50HullPoint second, Fs50HullPoint third) =>
        ((long)second.X - first.X) * (third.Y - first.Y)
        - ((long)second.Y - first.Y) * (third.X - first.X);
}
