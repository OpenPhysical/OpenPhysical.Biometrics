// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

/// <summary>Per-channel median of original background samples selected for complete removal.</summary>
/// <param name="Red">Median red value, rounded to the nearest even byte when necessary.</param>
/// <param name="Green">Median green value.</param>
/// <param name="Blue">Median blue value.</param>
/// <param name="SampleCount">Original fully removed pixels used for all three medians.</param>
public sealed record PortraitBackgroundColor(byte Red, byte Green, byte Blue, long SampleCount)
{
    /// <summary>Measures selected original RGB pixels; an empty region has no invented fill color.</summary>
    /// <param name="rgb">Original tightly packed RGB samples.</param>
    /// <param name="removed">Zero outside, one inside the fully removed region.</param>
    /// <returns>The median color, none for no samples, or an invalid buffer failure.</returns>
    public static Result<Maybe<PortraitBackgroundColor>, FacialError> Estimate(
        ReadOnlySpan<byte> rgb,
        ReadOnlySpan<byte> removed
    )
    {
        if (rgb.Length != (long)removed.Length * 3)
            return FacialErrors.InvalidPixelBuffer();
        var histogram = new[] { new long[256], new long[256], new long[256] };
        long count = 0;
        for (var i = 0; i < removed.Length; i++)
        {
            if (removed[i] > 1)
                return FacialErrors.InvalidPixelBuffer();
            if (removed[i] == 0)
                continue;
            count++;
            for (var c = 0; c < 3; c++)
                histogram[c][rgb[i * 3 + c]]++;
        }
        return count == 0
            ? Maybe<PortraitBackgroundColor>.None
            : Maybe.From(
                new PortraitBackgroundColor(
                    Median(histogram[0], count),
                    Median(histogram[1], count),
                    Median(histogram[2], count),
                    count
                )
            );
    }

    private static byte Median(long[] histogram, long count) =>
        (byte)
            Math.Round(
                (At(histogram, (count - 1) / 2) + At(histogram, count / 2)) / 2d,
                MidpointRounding.ToEven
            );

    private static int At(long[] histogram, long position)
    {
        long total = 0;
        for (var i = 0; i < 255; i++)
        {
            total += histogram[i];
            if (total > position)
                return i;
        }
        return 255;
    }
}
