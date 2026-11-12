// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

internal static class Fs50RidgeEvidence
{
    internal static bool Write(
        ReadOnlySpan<byte> pixels,
        byte[] mask,
        int[] positions,
        int count,
        Fs50EvidenceOptions options,
        byte[] output
    )
    {
        var (left, right, top, bottom) = Bounds(positions.AsSpan(0, count));
        var margin = Math.Max(17, Fs50RidgeProfile.SamplingMargin(options.MaximumParallelShift));
        Span<byte> evidence = stackalloc byte[50 * 47];
        evidence.Clear();
        for (
            var y = FirstCenter(Math.Max(margin, top));
            y <= Math.Min(749 - margin, bottom);
            y += 16
        )
        for (
            var x = FirstCenter(Math.Max(margin, left));
            x <= Math.Min(799 - margin, right);
            x += 16
        )
        {
            var tensor = Tensor(pixels, mask, x, y);
            if (tensor.Count < options.MinimumBlockCoverage * 1024)
                continue;
            if (Coherent(tensor.Xx, tensor.Yy, tensor.Xy, options))
                evidence[((y / 16) * 50) + (x / 16)] = Period(
                    pixels,
                    x,
                    y,
                    tensor.Xx,
                    tensor.Yy,
                    tensor.Xy,
                    options
                );
        }
        return WriteSupported(evidence, mask, output, options);
    }

    private static int FirstCenter(int minimum) => ((minimum + 7) / 16 * 16) + 8;

    private static (int Left, int Right, int Top, int Bottom) Bounds(ReadOnlySpan<int> positions)
    {
        int left = 800,
            right = 0,
            top = 750,
            bottom = 0;
        foreach (var index in positions)
        {
            left = Math.Min(left, index % 800);
            right = Math.Max(right, index % 800);
            top = Math.Min(top, index / 800);
            bottom = Math.Max(bottom, index / 800);
        }
        return (left, right, top, bottom);
    }

    private static bool WriteSupported(
        ReadOnlySpan<byte> evidence,
        byte[] mask,
        byte[] output,
        Fs50EvidenceOptions options
    )
    {
        var found = false;
        for (var y = 1; y < 46; y++)
        for (var x = 1; x < 49; x++)
        {
            if (evidence[(y * 50) + x] == 0)
                continue;
            found |= PatchAt(evidence, x, y, options);
            for (var row = (y * 16) - 8; row < (y * 16) + 24; row++)
            for (var column = (x * 16) - 8; column < (x * 16) + 24; column++)
                output[(row * 800) + column] = mask[(row * 800) + column];
        }
        return found;
    }

    private static bool PatchAt(
        ReadOnlySpan<byte> evidence,
        int x,
        int y,
        Fs50EvidenceOptions options
    )
    {
        var period = evidence[(y * 50) + x];
        if (period == 0)
            return false;
        var neighbors = 0;
        for (var row = Math.Max(0, y - 2); row <= Math.Min(46, y + 2); row++)
        for (var column = Math.Max(0, x - 2); column <= Math.Min(49, x + 2); column++)
            if (
                evidence[(row * 50) + column] > 0
                && Math.Abs(evidence[(row * 50) + column] - period) <= options.PeriodTolerance
            )
                neighbors++;
        return neighbors >= options.MinimumSupportedBlocks;
    }

    private static (double Xx, double Yy, double Xy, int Count) Tensor(
        ReadOnlySpan<byte> pixels,
        byte[] mask,
        int x,
        int y
    )
    {
        double xx = 0,
            yy = 0,
            xy = 0;
        var count = 0;
        for (var row = y - 16; row < y + 16; row++)
        for (var column = x - 16; column < x + 16; column++)
        {
            var index = (row * 800) + column;
            if (mask[index] == 0)
                continue;
            var (gx, gy) = Gradient(pixels, index);
            xx += gx * gx;
            yy += gy * gy;
            xy += gx * gy;
            count++;
        }
        return (xx, yy, xy, count);
    }

    private static (double X, double Y) Gradient(ReadOnlySpan<byte> pixels, int index) =>
        (
            pixels[index - 799]
                + (2 * pixels[index + 1])
                + pixels[index + 801]
                - pixels[index - 801]
                - (2 * pixels[index - 1])
                - pixels[index + 799],
            pixels[index + 799]
                + (2 * pixels[index + 800])
                + pixels[index + 801]
                - pixels[index - 801]
                - (2 * pixels[index - 800])
                - pixels[index - 799]
        );

    private static bool Coherent(double xx, double yy, double xy, Fs50EvidenceOptions options) =>
        xx + yy > 0
        && Math.Sqrt(((xx - yy) * (xx - yy)) + (4 * xy * xy))
            >= (xx + yy) * options.MinimumCoherence;

    private static byte Period(
        ReadOnlySpan<byte> pixels,
        int x,
        int y,
        double xx,
        double yy,
        double xy,
        Fs50EvidenceOptions options
    )
    {
        var angle = Math.Atan2(2 * xy, xx - yy) / 2;
        Span<double> profile = stackalloc double[32];
        if (!Fs50RidgeProfile.Create(pixels, x, y, angle, profile, options))
            return 0;
        double low = 255,
            high = 0;
        for (var i = 0; i < profile.Length; i++)
        {
            low = Math.Min(low, profile[i]);
            high = Math.Max(high, profile[i]);
        }
        return
            high - low >= options.MinimumProfileContrast
            && Transitions(profile, low, high) >= options.MinimumTransitions
            ? Fs50RidgePeriodicity.Measure(profile, options)
            : (byte)0;
    }

    private static int Transitions(ReadOnlySpan<double> profile, double low, double high)
    {
        int previous = 0,
            count = 0;
        foreach (var value in profile)
        {
            var state =
                value < low + ((high - low) * 0.35) ? -1
                : value > low + ((high - low) * 0.65) ? 1
                : 0;
            if (state == 0)
                continue;
            if (previous != 0 && previous != state)
                count++;
            previous = state;
        }
        return count;
    }
}
