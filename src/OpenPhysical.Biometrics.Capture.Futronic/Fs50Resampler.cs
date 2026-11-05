// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
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

        return ImmutableArray.CreateRange(output);
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
        Half value = (Half)((double)firstRow * (double)Fs50CubicCoefficients.Get(fractionY, 0));
        for (var row = 1; row < 4; row++)
        {
            var sample = Horizontal(raw, baseX, baseY + row - 1, fractionX);
            value = MultiplyAdd(sample, Fs50CubicCoefficients.Get(fractionY, row), value);
        }

        return Math.Clamp((int)Math.Round((double)value, MidpointRounding.ToEven), 0, 255);
    }

    private static Half Horizontal(ReadOnlySpan<byte> raw, int x, int y, int fraction)
    {
        Half value = (Half)(
            (double)Sample(raw, x - 1, y) * (double)Fs50CubicCoefficients.Get(fraction, 0)
        );
        for (var column = 1; column < 4; column++)
        {
            value = MultiplyAdd(
                (Half)Sample(raw, x + column - 1, y),
                Fs50CubicCoefficients.Get(fraction, column),
                value
            );
        }

        return value;
    }

    private static byte Sample(ReadOnlySpan<byte> raw, int x, int y) =>
        x >= 0 && x < Fs50Descriptor.RawWidth && y >= 0 && y < Fs50Descriptor.RawHeight
            ? raw[(y * Fs50Descriptor.RawWidth) + x]
            : (byte)0;

    // Double holds the exact product/sum of these bounded binary16 values before one Half rounding.
    private static Half MultiplyAdd(Half left, Half right, Half sum) =>
        (Half)(((double)left * (double)right) + (double)sum);
}
