// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

using CSharpFunctionalExtensions;

/// <summary>Measures unchanged RGB samples inside an explicit binary mask.</summary>
public static class RgbRegionMeasurements
{
    /// <summary>Measures selected samples and their native horizontal and vertical neighbors.</summary>
    /// <param name="rgb">Tightly packed RGB24 pixels.</param>
    /// <param name="width">Positive image width.</param>
    /// <param name="height">Positive image height.</param>
    /// <param name="mask">One byte per pixel, zero outside and one inside.</param>
    /// <param name="edgeThreshold">Absolute channel difference counted as an edge.</param>
    /// <returns>Finite evidence or an explicit dimension/buffer/mask failure.</returns>
    public static Result<RgbRegionStatistics, ImagingError> Measure(
        ReadOnlySpan<byte> rgb,
        int width,
        int height,
        ReadOnlySpan<byte> mask,
        double edgeThreshold = 16
    )
    {
        if (
            !RgbImage.DimensionsValid(width, height)
            || rgb.Length != (long)width * height * 3
            || mask.Length != (long)width * height
            || !double.IsFinite(edgeThreshold)
            || edgeThreshold is < 0 or > 255
        )
            return ImagingErrors.InvalidDimensions().Fail<RgbRegionStatistics>();
        var channels = new[]
        {
            new ChannelAccumulator(),
            new ChannelAccumulator(),
            new ChannelAccumulator(),
        };
        var cross = new double[3];
        for (var i = 0; i < mask.Length; i++)
        {
            if (mask[i] > 1)
                return ImagingErrors.InvalidDimensions().Fail<RgbRegionStatistics>();
            if (mask[i] == 0)
                continue;
            Add(rgb, i, channels, cross);
            if (i % width + 1 < width && mask[i + 1] == 1)
                Adjacent(rgb, i, i + 1, channels, edgeThreshold);
            if (i / width + 1 < height && mask[i + width] == 1)
                Adjacent(rgb, i, i + width, channels, edgeThreshold);
        }
        return Result(channels, cross);
    }

    private static bool Add(
        ReadOnlySpan<byte> rgb,
        int i,
        ChannelAccumulator[] channels,
        double[] cross
    )
    {
        for (var c = 0; c < 3; c++)
            channels[c].Add(rgb[i * 3 + c]);
        cross[0] += rgb[i * 3] * rgb[i * 3 + 1];
        cross[1] += rgb[i * 3] * rgb[i * 3 + 2];
        cross[2] += rgb[i * 3 + 1] * rgb[i * 3 + 2];
        return true;
    }

    private static bool Adjacent(
        ReadOnlySpan<byte> rgb,
        int a,
        int b,
        ChannelAccumulator[] channels,
        double threshold
    )
    {
        for (var c = 0; c < 3; c++)
            channels[c].Adjacent(Math.Abs(rgb[a * 3 + c] - rgb[b * 3 + c]), threshold);
        return true;
    }

    private static RgbRegionStatistics Result(ChannelAccumulator[] channels, double[] cross) =>
        new(
            channels[0].Count,
            channels[0].Statistics,
            channels[1].Statistics,
            channels[2].Statistics,
            Correlation(channels[0], channels[1], cross[0]),
            Correlation(channels[0], channels[2], cross[1]),
            Correlation(channels[1], channels[2], cross[2])
        );

    private static Maybe<double> Correlation(
        ChannelAccumulator a,
        ChannelAccumulator b,
        double cross
    ) =>
        a.Count > 1 && a.Variance > 0 && b.Variance > 0
            ? Maybe.From(
                Math.Clamp(
                    (cross / a.Count - a.Mean * b.Mean) / Math.Sqrt(a.Variance * b.Variance),
                    -1,
                    1
                )
            )
            : Maybe<double>.None;
}
