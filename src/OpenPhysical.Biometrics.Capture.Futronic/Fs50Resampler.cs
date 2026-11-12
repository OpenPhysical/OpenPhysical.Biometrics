// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

/// <summary>Managed cubic remapping and explicit grayscale inversion; input pixels are unchanged.</summary>
public static class Fs50Resampler
{
    /// <summary>Stable numerical profile; changing interpolation requires a different identifier.</summary>
    public const string ProfileId = "fs50-cubic-half128-v1";

    /// <summary>SHA-256 of the recovered coefficient bit patterns in little-endian order.</summary>
    public const string CoefficientsSha256 = Fs50CubicCoefficients.Sha256;

    /// <summary>Produces an 800 by 750 image from a complete raw frame and validated map.</summary>
    /// <param name="raw">Complete, uncorrected grayscale sensor bytes.</param>
    /// <param name="map">Validated factory map.</param>
    /// <returns>Inverted corrected pixels or an incomplete-frame failure.</returns>
    public static Result<ImmutableArray<byte>, CaptureError> Correct(
        ReadOnlySpan<byte> raw,
        Fs50GeometryMap map
    )
    {
        if (raw.Length != Fs50Descriptor.RawWidth * Fs50Descriptor.RawHeight)
        {
            return Result.Failure<ImmutableArray<byte>, CaptureError>(
                new CaptureError(CaptureErrorCodes.TransferIncomplete, "Incomplete FS50 raw frame.")
            );
        }

        var output = new byte[Fs50Descriptor.Width * Fs50Descriptor.Height];
        for (var i = 0; i < output.Length; i++)
        {
            var point = map.PointAt(i);
            output[i] = (byte)(255 - Interpolate(raw, point.X, point.Y));
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(output);
    }

    private static int Interpolate(ReadOnlySpan<byte> raw, float x, float y)
    {
        // Factory fixed-point maps contain exact multiples of 1/128; no arbitrary float maps enter here.
        int fixedX = (int)(x * 128);
        int fixedY = (int)(y * 128);
        int baseX = fixedX >> 7;
        int baseY = fixedY >> 7;
        int fractionX = fixedX & 127;
        int fractionY = fixedY & 127;
        var firstRow = Horizontal(raw, baseX, baseY - 1, fractionX);
        double value = RoundToHalf(firstRow * Fs50CubicCoefficients.GetExact(fractionY, 0));
        for (var row = 1; row < 4; row++)
        {
            var sample = Horizontal(raw, baseX, baseY + row - 1, fractionX);
            value = MultiplyAdd(sample, Fs50CubicCoefficients.GetExact(fractionY, row), value);
        }

        return Math.Clamp((int)Math.Round(value, MidpointRounding.ToEven), 0, 255);
    }

    private static double Horizontal(ReadOnlySpan<byte> raw, int x, int y, int fraction)
    {
        if (x >= 1 && x < Fs50Descriptor.RawWidth - 2 && y >= 0 && y < Fs50Descriptor.RawHeight)
            return InteriorHorizontal(raw, (y * Fs50Descriptor.RawWidth) + x, fraction);
        double value = RoundToHalf(
            Sample(raw, x - 1, y) * Fs50CubicCoefficients.GetExact(fraction, 0)
        );
        value = MultiplyAdd(Sample(raw, x, y), Fs50CubicCoefficients.GetExact(fraction, 1), value);
        value = MultiplyAdd(
            Sample(raw, x + 1, y),
            Fs50CubicCoefficients.GetExact(fraction, 2),
            value
        );
        return MultiplyAdd(
            Sample(raw, x + 2, y),
            Fs50CubicCoefficients.GetExact(fraction, 3),
            value
        );
    }

    private static double InteriorHorizontal(ReadOnlySpan<byte> raw, int offset, int fraction)
    {
        double value = RoundToHalf(raw[offset - 1] * Fs50CubicCoefficients.GetExact(fraction, 0));
        value = MultiplyAdd(raw[offset], Fs50CubicCoefficients.GetExact(fraction, 1), value);
        value = MultiplyAdd(raw[offset + 1], Fs50CubicCoefficients.GetExact(fraction, 2), value);
        return MultiplyAdd(raw[offset + 2], Fs50CubicCoefficients.GetExact(fraction, 3), value);
    }

    private static byte Sample(ReadOnlySpan<byte> raw, int x, int y) =>
        x >= 0 && x < Fs50Descriptor.RawWidth && y >= 0 && y < Fs50Descriptor.RawHeight
            ? raw[(y * Fs50Descriptor.RawWidth) + x]
            : (byte)0;

    // Double holds the exact product/sum of these bounded binary16 values before one Half rounding.
    private static double MultiplyAdd(double left, double right, double sum) =>
        RoundToHalf((left * right) + sum);

    private static double RoundToHalf(double value)
    {
        // These byte/weight sums stay finite and far below binary16 overflow.
        // Keep the exact binary16 value in double form without repeated Half conversions.
        long bits = BitConverter.DoubleToInt64Bits(value);
        if ((bits & 0x7FF0000000000000L) < 0x3F10000000000000L)
            return Math.Round(value * 16777216, MidpointRounding.ToEven) / 16777216;
        const long discarded = (1L << 42) - 1;
        long rounding = (1L << 41) - 1 + ((bits >> 42) & 1);
        return BitConverter.Int64BitsToDouble((bits + rounding) & ~discarded);
    }
}
