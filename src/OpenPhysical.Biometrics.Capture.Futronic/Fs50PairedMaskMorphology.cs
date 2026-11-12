// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

internal static class Fs50PairedMaskMorphology
{
    private const int Stride = 801;
    private const int RoleBits = 20;
    private const long WeakMask = (1L << RoleBits) - 1;

    internal static bool Apply(
        byte[] strong,
        byte[] weak,
        long[] integral,
        Fs50EvidenceOptions options
    )
    {
        using var first = new Fs50TemporaryPlane();
        using var second = new Fs50TemporaryPlane();
        var closing = options.ClosingRadius;
        var opening = options.OpeningRadius;
        _ = Morph(strong, weak, first.Buffer, second.Buffer, integral, closing, true);
        _ = Morph(first.Buffer, second.Buffer, strong, weak, integral, closing, false);
        _ = Morph(strong, weak, first.Buffer, second.Buffer, integral, opening, false);
        _ = Morph(first.Buffer, second.Buffer, strong, weak, integral, opening, true);
        return true;
    }

    private static bool Morph(
        byte[] strong,
        byte[] weak,
        byte[] outputStrong,
        byte[] outputWeak,
        long[] integral,
        int radius,
        bool dilate
    )
    {
        _ = Integrate(strong, weak, integral);
        for (var y = 0; y < 750; y++)
        for (var x = 0; x < 800; x++)
        {
            int left = Math.Max(0, x - radius),
                top = Math.Max(0, y - radius);
            int right = Math.Min(800, x + radius + 1),
                bottom = Math.Min(750, y + radius + 1);
            var count =
                integral[bottom * Stride + right]
                - integral[top * Stride + right]
                - integral[bottom * Stride + left]
                + integral[top * Stride + left];
            var strongCount = count >> RoleBits;
            var weakCount = count & WeakMask;
            var area = (right - left) * (bottom - top);
            var index = y * 800 + x;
            outputStrong[index] = (byte)((dilate ? strongCount > 0 : strongCount == area) ? 1 : 0);
            outputWeak[index] = (byte)((dilate ? weakCount > 0 : weakCount == area) ? 1 : 0);
        }
        return true;
    }

    private static bool Integrate(byte[] strong, byte[] weak, long[] integral)
    {
        // Each role has at most 600000 ones, so the low 20 bits cannot carry into the other role.
        Array.Clear(integral, 0, Stride);
        for (var y = 0; y < 750; y++)
        {
            long sum = 0;
            var row = (y + 1) * Stride;
            integral[row] = 0;
            for (var x = 0; x < 800; x++)
            {
                var input = y * 800 + x;
                sum += weak[input] + ((long)strong[input] << RoleBits);
                integral[row + x + 1] = integral[row - Stride + x + 1] + sum;
            }
        }
        return true;
    }
}
