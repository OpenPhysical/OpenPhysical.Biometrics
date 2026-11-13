// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

internal static class PortraitAlphaRefinement
{
    internal static float[] Contract(float[] alpha, int width, int height, double radius)
    {
        var offsets = Enumerable
            .Range(0, 16)
            .Select(i =>
                (X: radius * Math.Cos(i * Math.Tau / 16), Y: radius * Math.Sin(i * Math.Tau / 16))
            )
            .ToArray();
        var result = new float[alpha.Length];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var minimum = alpha[y * width + x];
            foreach (var offset in offsets)
                minimum = Math.Min(
                    minimum,
                    Sample(alpha, width, height, x + offset.X, y + offset.Y)
                );
            result[y * width + x] = minimum;
        }
        return result;
    }

    private static float Sample(float[] alpha, int width, int height, double x, double y)
    {
        x = Math.Clamp(x, 0, width - 1);
        y = Math.Clamp(y, 0, height - 1);
        var left = (int)x;
        var top = (int)y;
        var right = Math.Min(left + 1, width - 1);
        var bottom = Math.Min(top + 1, height - 1);
        var upper =
            alpha[top * width + left] * (1 - (x - left)) + alpha[top * width + right] * (x - left);
        var lower =
            alpha[bottom * width + left] * (1 - (x - left))
            + alpha[bottom * width + right] * (x - left);
        return (float)(upper * (1 - (y - top)) + lower * (y - top));
    }

    internal static float[] Smooth(float[] alpha, int width, int height, int radius)
    {
        if (radius == 0)
            return alpha.ToArray();
        var sigma = radius / 2d + .25;
        var kernel = Enumerable
            .Range(-radius, radius * 2 + 1)
            .Select(i => Math.Exp(-(double)(i * i) / (2 * sigma * sigma)))
            .ToArray();
        var total = kernel.Sum();
        kernel = kernel.Select(value => value / total).ToArray();
        var horizontal = new float[alpha.Length];
        var result = new float[alpha.Length];
        Convolve(alpha, horizontal, width, height, kernel, true);
        Convolve(horizontal, result, width, height, kernel, false);
        Array.Clear(horizontal);
        return result;
    }

    private static bool Convolve(
        float[] source,
        float[] result,
        int width,
        int height,
        double[] kernel,
        bool horizontal
    )
    {
        var radius = kernel.Length / 2;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            double sum = 0;
            for (var i = -radius; i <= radius; i++)
            {
                var sx = horizontal ? Math.Clamp(x + i, 0, width - 1) : x;
                var sy = horizontal ? y : Math.Clamp(y + i, 0, height - 1);
                sum += source[sy * width + sx] * kernel[i + radius];
            }
            result[y * width + x] = (float)sum;
        }
        return true;
    }
}
