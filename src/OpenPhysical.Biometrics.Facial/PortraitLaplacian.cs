// SPDX-License-Identifier: GPL-2.0-or-later
// Adapted from FaceOFFx masked LoG analysis; see Notices/FaceOFFx-MIT.txt.
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

internal static class PortraitLaplacian
{
    private static readonly double[] Kernel = CreateKernel();

    internal static Maybe<double> Measure(
        ReadOnlySpan<byte> rgb,
        int width,
        int height,
        byte[] mask
    )
    {
        var gray = new double[width * height];
        for (var i = 0; i < gray.Length; i++)
            gray[i] = (.2126 * rgb[i * 3] + .7152 * rgb[i * 3 + 1] + .0722 * rgb[i * 3 + 2]) / 255;
        var mean = gray.Average();
        var deviation = Math.Sqrt(gray.Sum(value => (value - mean) * (value - mean)) / gray.Length);
        if (deviation < .0001)
        {
            Array.Clear(gray);
            return Maybe<double>.None;
        }
        for (var i = 0; i < gray.Length; i++)
            gray[i] = (gray[i] - mean) / deviation;
        double sum = 0,
            square = 0;
        long count = 0;
        for (var y = 4; y < height - 4; y++)
        for (var x = 4; x < width - 4; x++)
        {
            if (mask[y * width + x] == 0)
                continue;
            var response = Response(gray, width, x, y);
            count++;
            sum += response;
            square += response * response;
        }
        Array.Clear(gray);
        return count < 2
            ? Maybe<double>.None
            : Math.Max(0, square / count - Math.Pow(sum / count, 2));
    }

    private static double Response(double[] gray, int width, int x, int y)
    {
        double response = 0;
        for (var ky = -4; ky <= 4; ky++)
        for (var kx = -4; kx <= 4; kx++)
            response += gray[(y + ky) * width + x + kx] * Kernel[(ky + 4) * 9 + kx + 4];
        return response;
    }

    private static double[] CreateKernel()
    {
        const double variance = 1.4 * 1.4;
        var kernel = new double[81];
        for (var y = -4; y <= 4; y++)
        for (var x = -4; x <= 4; x++)
        {
            var radius = x * x + y * y;
            kernel[(y + 4) * 9 + x + 4] =
                -(1 / (Math.PI * variance * variance))
                * (1 - radius / (2 * variance))
                * Math.Exp(-radius / (2 * variance));
        }
        var mean = kernel.Average();
        for (var i = 0; i < kernel.Length; i++)
            kernel[i] -= mean;
        return kernel;
    }
}
