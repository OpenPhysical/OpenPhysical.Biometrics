// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

internal static class Fs50TextureStatistics
{
    internal static bool Fill(
        ReadOnlySpan<byte> pixels,
        long[] sums,
        long[] squares,
        byte[] strong,
        byte[] weak,
        Fs50EvidenceOptions options
    )
    {
        _ = Integrate(pixels, sums, squares);
        for (var y = 0; y < 750; y++)
        for (var x = 0; x < 800; x++)
        {
            double mean = Rectangle(sums, x, y) / 225.0;
            double variance = Rectangle(squares, x, y) / 225.0 - mean * mean;
            var primary = variance > options.TextureVariance && mean < options.TextureMean;
            strong[y * 800 + x] = (byte)(primary ? 1 : 0);
            weak[y * 800 + x] = (byte)(
                primary || variance > options.FaintVariance && mean < options.FaintMean ? 1 : 0
            );
        }
        return true;
    }

    private static bool Integrate(ReadOnlySpan<byte> pixels, long[] sums, long[] squares)
    {
        const int stride = 815;
        Array.Clear(sums, 0, stride);
        Array.Clear(squares, 0, stride);
        for (var y = 0; y < 764; y++)
        {
            long sum = 0,
                square = 0;
            var row = (y + 1) * stride;
            sums[row] = 0;
            squares[row] = 0;
            for (var x = 0; x < 814; x++)
            {
                var value = pixels[Reflect(y - 7, 750) * 800 + Reflect(x - 7, 800)];
                sum += value;
                square += value * value;
                var index = row + x + 1;
                sums[index] = sums[index - stride] + sum;
                squares[index] = squares[index - stride] + square;
            }
        }
        return true;
    }

    private static long Rectangle(long[] values, int x, int y) =>
        values[(y + 15) * 815 + x + 15]
        - values[y * 815 + x + 15]
        - values[(y + 15) * 815 + x]
        + values[y * 815 + x];

    private static int Reflect(int index, int size) =>
        index < 0 ? -index
        : index >= size ? 2 * size - index - 2
        : index;
}
