// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Finds substantial textured contact while rejecting isolated marks on glass.</summary>
public static class Fs50ContactAnalysis
{
    /// <summary>Analyzes unchanged corrected pixels using local variance and connected area.</summary>
    /// <param name="pixels">Complete 800 by 750 corrected image.</param>
    /// <returns>Contact statistics or a geometry failure.</returns>
    public static Result<Fs50ContactRegion, CaptureError> Analyze(ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length != Fs50Descriptor.Width * Fs50Descriptor.Height)
            return new CaptureError(
                CaptureErrorCodes.UnsupportedGeometry,
                "Contact analysis requires a complete corrected FS50 image."
            );
        var mask = LargestComponent(Fs50ContactMask.Texture(pixels));
        var histogram = new int[256];
        int area = 0;
        for (int i = 0; i < mask.Length; i++)
            if (mask[i] != 0)
            {
                area++;
                histogram[pixels[i]]++;
            }
        double low = Percentile(histogram, area, 0.05);
        double high = Percentile(histogram, area, 0.95);
        return new Fs50ContactRegion(mask, area, high - low, area == 0 ? 0 : 255 - low);
    }

    private static byte[] LargestComponent(byte[] mask)
    {
        var queue = new int[mask.Length];
        var best = Array.Empty<int>();
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] == 0)
                continue;
            var component = Flood(mask, queue, i);
            if (component.Length > best.Length)
                best = component;
        }
        var output = new byte[mask.Length];
        if (best.Length < 4000)
            return output;
        int minX = 800,
            maxX = 0,
            minY = 750,
            maxY = 0;
        foreach (var index in best)
        {
            minX = Math.Min(minX, index % 800);
            maxX = Math.Max(maxX, index % 800);
            minY = Math.Min(minY, index / 800);
            maxY = Math.Max(maxY, index / 800);
        }
        if (maxX - minX + 1 < 40 || maxY - minY + 1 < 80)
            return output;
        foreach (var index in best)
            output[index] = 1;
        return output;
    }

    private static int[] Flood(byte[] mask, int[] queue, int start)
    {
        int head = 0,
            tail = 1;
        queue[0] = start;
        mask[start] = 0;
        while (head < tail)
        {
            int index = queue[head++],
                x = index % 800,
                y = index / 800;
            for (int yy = Math.Max(0, y - 1); yy <= Math.Min(749, y + 1); yy++)
            for (int xx = Math.Max(0, x - 1); xx <= Math.Min(799, x + 1); xx++)
            {
                int next = (yy * 800) + xx;
                if (mask[next] == 0)
                    continue;
                mask[next] = 0;
                queue[tail++] = next;
            }
        }
        return queue.AsSpan(0, tail).ToArray();
    }

    private static double Percentile(int[] histogram, int count, double fraction)
    {
        if (count == 0)
            return 0;
        double position = (count - 1) * fraction;
        int lower = (int)position,
            upper = (int)Math.Ceiling(position);
        int seen = 0,
            low = 0;
        for (int i = 0; i < histogram.Length; i++)
        {
            int next = seen + histogram[i];
            if (seen <= lower && next > lower)
                low = i;
            if (next > upper)
                return low + ((i - low) * (position - lower));
            seen = next;
        }
        return 255;
    }
}
