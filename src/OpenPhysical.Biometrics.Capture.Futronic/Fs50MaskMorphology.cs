// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers;

internal static class Fs50MaskMorphology
{
    internal static byte[] Apply(byte[] mask, Fs50EvidenceOptions options)
    {
        var temporary = ArrayPool<byte>.Shared.Rent(mask.Length);
        var integral = ArrayPool<int>.Shared.Rent(801 * 751);
        try
        {
            _ = Morph(mask, temporary, integral, options.ClosingRadius, true);
            _ = Morph(temporary, mask, integral, options.ClosingRadius, false);
            _ = Morph(mask, temporary, integral, options.OpeningRadius, false);
            _ = Morph(temporary, mask, integral, options.OpeningRadius, true);
            return mask;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(temporary, clearArray: true);
            ArrayPool<int>.Shared.Return(integral, clearArray: true);
        }
    }

    internal static byte[] Expand(byte[] mask, int radius) => Transform(mask, radius, true);

    internal static bool ExpandInto(byte[] mask, int radius, byte[] output) =>
        TransformInto(mask, output, radius, true);

    internal static byte[] Inner(byte[] mask, int radius)
    {
        var output = new byte[800 * 750];
        _ = InnerInto(mask, radius, output);
        return output;
    }

    internal static bool InnerInto(byte[] mask, int radius, byte[] output)
    {
        _ = TransformInto(mask, output, radius, false);
        for (var y = 0; y < 750; y++)
        for (var x = 0; x < 800; x++)
            if (x < radius || y < radius || x >= 800 - radius || y >= 750 - radius)
                output[(y * 800) + x] = 0;
        return true;
    }

    internal static byte[] InnerAtCuts(byte[] mask, int radius) => Transform(mask, radius, false);

    private static byte[] Transform(byte[] mask, int radius, bool dilate)
    {
        var output = new byte[800 * 750];
        _ = TransformInto(mask, output, radius, dilate);
        return output;
    }

    private static bool TransformInto(byte[] mask, byte[] output, int radius, bool dilate)
    {
        var integral = ArrayPool<int>.Shared.Rent(801 * 751);
        try
        {
            _ = Morph(mask, output, integral, radius, dilate);
            return true;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(integral, clearArray: true);
        }
    }

    private static bool Morph(byte[] mask, byte[] output, int[] integral, int radius, bool dilate)
    {
        _ = Integrate(mask, integral);
        for (var y = 0; y < 750; y++)
        for (var x = 0; x < 800; x++)
        {
            int left = Math.Max(0, x - radius),
                top = Math.Max(0, y - radius);
            int right = Math.Min(800, x + radius + 1),
                bottom = Math.Min(750, y + radius + 1);
            var count =
                integral[bottom * 801 + right]
                - integral[top * 801 + right]
                - integral[bottom * 801 + left]
                + integral[top * 801 + left];
            output[y * 800 + x] = (byte)(
                (dilate ? count > 0 : count == (right - left) * (bottom - top)) ? 1 : 0
            );
        }
        return true;
    }

    private static bool Integrate(byte[] mask, int[] integral)
    {
        Array.Clear(integral, 0, 801);
        for (var y = 0; y < 750; y++)
        {
            var sum = 0;
            var row = (y + 1) * 801;
            integral[row] = 0;
            for (var x = 0; x < 800; x++)
            {
                sum += mask[y * 800 + x];
                integral[row + x + 1] = integral[row - 801 + x + 1] + sum;
            }
        }
        return true;
    }
}
