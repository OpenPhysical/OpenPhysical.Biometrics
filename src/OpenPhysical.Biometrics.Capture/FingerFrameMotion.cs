// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

internal static class FingerFrameMotion
{
    internal static double Score(FingerPresenceSample first, FingerPresenceSample second)
    {
        if (first.Frame.Width != second.Frame.Width || first.Frame.Height != second.Frame.Height)
            return double.PositiveInfinity;
        double a = 0,
            b = 0,
            aa = 0,
            bb = 0;
        var count = 0;
        foreach (var (i, j) in Overlap(first, second))
        {
            var x = first.Frame.Pixels[i];
            var y = second.Frame.Pixels[j];
            a += x;
            b += y;
            aa += x * x;
            bb += y * y;
            count++;
        }
        if (count == 0)
            return double.PositiveInfinity;
        a /= count;
        b /= count;
        var sa = Math.Max(1, Math.Sqrt(Math.Max(0, aa / count - a * a)));
        var sb = Math.Max(1, Math.Sqrt(Math.Max(0, bb / count - b * b)));
        double difference = 0;
        foreach (var (i, j) in Overlap(first, second))
            difference += Math.Abs(
                (first.Frame.Pixels[i] - a) / sa - (second.Frame.Pixels[j] - b) / sb
            );
        return difference / count;
    }

    private static IEnumerable<(int First, int Second)> Overlap(
        FingerPresenceSample first,
        FingerPresenceSample second
    )
    {
        var dx = (int)Math.Round(second.Geometry.X - first.Geometry.X);
        var dy = (int)Math.Round(second.Geometry.Y - first.Geometry.Y);
        var width = first.Frame.Width;
        var height = first.Frame.Height;
        var stride = width < 8 || height < 8 ? 1 : 4;
        for (var y = 0; y < height; y += stride)
        for (var x = 0; x < width; x += stride)
        {
            var nx = x + dx;
            var ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                continue;
            var i = y * width + x;
            var j = ny * width + nx;
            if (first.Mask[i] != 0 && second.Mask[j] != 0)
                yield return (i, j);
        }
    }
}
