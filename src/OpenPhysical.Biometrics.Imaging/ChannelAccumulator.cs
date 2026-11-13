// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

using CSharpFunctionalExtensions;

internal sealed class ChannelAccumulator
{
    private readonly long[] histogram = new long[16];
    private double sum,
        square,
        gradient;
    private long pairs,
        edges;
    internal long Count { get; private set; }
    internal double Mean => Count == 0 ? 0 : sum / Count;
    internal double Variance => Count == 0 ? 0 : Math.Max(0, square / Count - Mean * Mean);
    internal Maybe<PixelChannelStatistics> Statistics =>
        Count == 0
            ? Maybe<PixelChannelStatistics>.None
            : new PixelChannelStatistics(
                Mean,
                Variance,
                Entropy(),
                pairs == 0 ? 0 : gradient / pairs,
                pairs == 0 ? 0 : (double)edges / pairs
            );

    internal bool Add(byte value)
    {
        Count++;
        sum += value;
        square += value * value;
        histogram[value / 16]++;
        return true;
    }

    internal bool Adjacent(double difference, double threshold)
    {
        pairs++;
        gradient += difference;
        if (difference > threshold)
            edges++;
        return true;
    }

    private double Entropy() =>
        histogram
            .Where(value => value > 0)
            .Sum(value => -(double)value / Count * Math.Log2((double)value / Count));
}
