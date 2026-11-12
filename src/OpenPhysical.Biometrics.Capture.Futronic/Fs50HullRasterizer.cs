// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class Fs50HullRasterizer
{
    internal static Result<byte[], CaptureError> Draw(
        ImmutableArray<Fs50HullPoint> hull,
        CancellationToken cancellationToken
    )
    {
        var mask = new byte[800 * 750];
        for (var y = 0; y < 750; y++)
        {
            if (cancellationToken.IsCancellationRequested)
                return Fs50MaskErrors.Cancelled;
            var (left, right) = Intersections(hull, 2 * y);
            if (!double.IsFinite(left))
                continue;
            var first = Math.Max(0, (int)Math.Ceiling(left / 2));
            var last = Math.Min(799, (int)Math.Floor(right / 2));
            if (first <= last)
                mask.AsSpan(y * 800 + first, last - first + 1).Fill(1);
        }
        return mask;
    }

    private static (double Left, double Right) Intersections(
        ImmutableArray<Fs50HullPoint> hull,
        int y
    )
    {
        double left = double.PositiveInfinity,
            right = double.NegativeInfinity;
        for (var i = 0; i < hull.Length; i++)
        {
            var first = hull[i];
            var second = hull[(i + 1) % hull.Length];
            // Cell-corner Y values are odd; pixel-center rows are even, so vertices never straddle a row.
            if (y <= Math.Min(first.Y, second.Y) || y >= Math.Max(first.Y, second.Y))
                continue;
            var x =
                first.X
                + ((long)(second.X - first.X) * (y - first.Y) / (double)(second.Y - first.Y));
            left = Math.Min(left, x);
            right = Math.Max(right, x);
        }
        return (left, right);
    }
}
