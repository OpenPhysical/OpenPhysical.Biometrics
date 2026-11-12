// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

internal static class Fs50EmptySensorDifference
{
    internal static double MeanGray(ReadOnlySpan<byte> pixels)
    {
        long sum = 0;
        foreach (var pixel in pixels)
            sum += pixel;
        return sum / (double)pixels.Length;
    }

    internal static double Mean(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> mask
    )
    {
        long difference = 0;
        var count = 0;
        for (var i = 0; i < first.Length; i++)
        {
            if (!mask.IsEmpty && mask[i] == 0)
                continue;
            difference += Math.Abs(first[i] - second[i]);
            count++;
        }
        return count == 0 ? 0 : difference / (double)count;
    }
}
