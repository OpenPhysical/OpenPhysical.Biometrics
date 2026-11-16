// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Unenhanced photometric evidence for a specific observed image region.</summary>
public sealed record Fs50RegionStatistics
{
    private Fs50RegionStatistics(int area, double low, double high)
    {
        Area = area;
        LowGray = low;
        HighGray = high;
    }

    /// <summary>Gets observed pixel count.</summary>
    public int Area { get; }

    /// <summary>Gets interpolated fifth-percentile gray.</summary>
    public double LowGray { get; }

    /// <summary>Gets interpolated 95th-percentile gray.</summary>
    public double HighGray { get; }

    /// <summary>Gets percentile contrast.</summary>
    public double Contrast => HighGray - LowGray;

    /// <summary>Gets inverted fifth-percentile intensity, zero for an empty region.</summary>
    public double Signal => Area == 0 ? 0 : 255 - LowGray;

    internal static Fs50RegionStatistics Positions(
        ReadOnlySpan<byte> pixels,
        ReadOnlySpan<int> positions
    )
    {
        Span<int> histogram = stackalloc int[256];
        histogram.Clear();
        foreach (var index in positions)
            histogram[pixels[index]]++;
        return Create(histogram, positions.Length);
    }

    internal static Fs50RegionStatistics Mask(ReadOnlySpan<byte> pixels, ReadOnlySpan<byte> mask)
    {
        Span<int> histogram = stackalloc int[256];
        histogram.Clear();
        var count = 0;
        for (var i = 0; i < pixels.Length; i++)
            if (mask[i] != 0)
            {
                histogram[pixels[i]]++;
                count++;
            }
        return Create(histogram, count);
    }

    private static Fs50RegionStatistics Create(ReadOnlySpan<int> histogram, int count) =>
        new(count, Percentile(histogram, count, 0.05), Percentile(histogram, count, 0.95));

    private static double Percentile(ReadOnlySpan<int> histogram, int count, double fraction)
    {
        if (count == 0)
            return 0;
        double position = (count - 1) * fraction;
        int lower = (int)position,
            upper = (int)Math.Ceiling(position),
            seen = 0,
            low = 0;
        for (var i = 0; i < histogram.Length; i++)
        {
            var next = seen + histogram[i];
            if (seen <= lower && next > lower)
                low = i;
            if (next > upper)
                return low + ((i - low) * (position - lower));
            seen = next;
        }
        return 255;
    }
}
