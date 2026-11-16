// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;

internal sealed record CaptureRegionMeasurements(double X, double Y, double Diagonal, int Area)
{
    internal static CaptureRegionMeasurements Measure(
        CaptureGeometry geometry,
        ImmutableArray<byte> mask
    )
    {
        long sumX = 0,
            sumY = 0;
        int area = 0,
            left = geometry.Width,
            top = geometry.Height,
            right = 0,
            bottom = 0;
        for (var index = 0; index < mask.Length; index++)
        {
            if (mask[index] == 0)
                continue;
            int x = index % geometry.Width,
                y = index / geometry.Width;
            area++;
            sumX += x;
            sumY += y;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }
        return area == 0
            ? new(0, 0, 0, 0)
            : new(
                (double)sumX / area,
                (double)sumY / area,
                Math.Sqrt(Math.Pow(right - left + 1, 2) + Math.Pow(bottom - top + 1, 2)),
                area
            );
    }
}
