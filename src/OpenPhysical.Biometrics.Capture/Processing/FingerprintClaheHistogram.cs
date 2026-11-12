// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

internal static class FingerprintClaheHistogram
{
    internal static bool Fill(
        ReadOnlySpan<byte> pixels,
        FingerprintProcessingMask mask,
        FingerprintPreprocessingPolicy policy,
        int tileX,
        int tileY,
        Span<byte> lookup
    )
    {
        Span<int> histogram = stackalloc int[256];
        histogram.Clear();
        var count = Count(pixels, mask, policy.TileSize, tileX, tileY, histogram);
        if (count == 0)
        {
            for (var i = 0; i < lookup.Length; i++)
                lookup[i] = (byte)i;
            return true;
        }
        _ = Clip(histogram, Math.Max(1, (int)(policy.ClipLimit * count / 256)));
        var cumulative = 0;
        for (var i = 0; i < lookup.Length; i++)
        {
            cumulative += histogram[i];
            lookup[i] = (byte)Math.Clamp(Math.Round(cumulative * 255.0 / count), 0, 255);
        }
        return true;
    }

    private static int Count(
        ReadOnlySpan<byte> pixels,
        FingerprintProcessingMask mask,
        int tileSize,
        int tileX,
        int tileY,
        Span<int> histogram
    )
    {
        var count = 0;
        int bottom = Math.Min(mask.Height, (tileY + 1) * tileSize),
            right = Math.Min(mask.Width, (tileX + 1) * tileSize);
        for (var y = tileY * tileSize; y < bottom; y++)
        for (var x = tileX * tileSize; x < right; x++)
        {
            var index = (y * mask.Width) + x;
            if (mask.Retained[index] == 0)
                continue;
            histogram[pixels[index]]++;
            count++;
        }
        return count;
    }

    private static bool Clip(Span<int> histogram, int limit)
    {
        var excess = 0;
        for (var i = 0; i < histogram.Length; i++)
        {
            excess += Math.Max(0, histogram[i] - limit);
            histogram[i] = Math.Min(histogram[i], limit);
        }
        var uniform = excess / histogram.Length;
        var residual = excess % histogram.Length;
        for (var i = 0; i < histogram.Length; i++)
            histogram[i] += uniform;
        if (residual == 0)
            return true;
        var step = Math.Max(1, histogram.Length / residual);
        for (var i = 0; i < histogram.Length && residual > 0; i += step, residual--)
            histogram[i]++;
        return true;
    }
}
