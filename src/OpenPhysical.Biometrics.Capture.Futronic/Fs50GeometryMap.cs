// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>A bounded, orientation-preserving factory polynomial mapping.</summary>
public sealed class Fs50GeometryMap
{
    private readonly float[] mapX;
    private readonly float[] mapY;

    private Fs50GeometryMap(float[] mapX, float[] mapY)
    {
        this.mapX = mapX;
        this.mapY = mapY;
    }

    /// <summary>Builds and checks every sample and local Jacobian of the mapping.</summary>
    /// <param name="calibration">Validated factory inputs.</param>
    /// <returns>A checked map or an invalid-calibration failure.</returns>
    public static Result<Fs50GeometryMap, CaptureError> Create(Fs50Calibration calibration)
    {
        var coefficients = new long[12];
        for (var i = 0; i < coefficients.Length; i++)
        {
            // Signed division truncates toward zero; signed shifts preserve vendor flooring.
            long value = calibration.Coefficients[i];
            long first = i % 6 < 3 ? value * 32 / 25 : value * 4096 / 3125;
            coefficients[i] = first * 32 / 25;
        }

        var coordinates = BuildCoordinates(calibration, coefficients);
        var map = new Fs50GeometryMap(coordinates.X, coordinates.Y);
        return map.IsValid()
            ? Result.Success<Fs50GeometryMap, CaptureError>(map)
            : Result.Failure<Fs50GeometryMap, CaptureError>(
                new CaptureError(
                    CaptureErrorCodes.CalibrationInvalid,
                    "FS50 mapping is folded, implausible, or outside the sensor."
                )
            );
    }

    /// <summary>Gets a source coordinate for a valid output position.</summary>
    /// <param name="x">Output column.</param>
    /// <param name="y">Output row.</param>
    /// <returns>The source coordinate or an out-of-range failure.</returns>
    public Result<(float X, float Y), CaptureError> GetPoint(int x, int y)
    {
        if (x < 0 || x >= Fs50Descriptor.Width || y < 0 || y >= Fs50Descriptor.Height)
        {
            return Result.Failure<(float X, float Y), CaptureError>(
                new CaptureError(
                    CaptureErrorCodes.InvalidArgument,
                    "Output coordinate is outside the image."
                )
            );
        }

        var index = (y * Fs50Descriptor.Width) + x;
        return (mapX[index], mapY[index]);
    }

    internal (float X, float Y) PointAt(int index) => (mapX[index], mapY[index]);

    private static (float[] X, float[] Y) BuildCoordinates(Fs50Calibration calibration, long[] c)
    {
        var xMap = new float[Fs50Descriptor.Width * Fs50Descriptor.Height];
        var yMap = new float[xMap.Length];
        for (var row = 0; row < Fs50Descriptor.Height; row++)
        {
            long y = row - 416 + calibration.OffsetY;
            for (var column = 0; column < Fs50Descriptor.Width; column++)
            {
                long x = column - 640 + calibration.OffsetX;
                var index = (row * Fs50Descriptor.Width) + column;
                xMap[index] = Component(c, 0, x, y, 640);
                yMap[index] = Component(c, 6, x, y, 416);
            }
        }

        return (xMap, yMap);
    }

    private static float Component(long[] c, int start, long x, long y, int center)
    {
        long quadratic =
            ((c[start + 3] * x * x) + (c[start + 4] * x * y) + (c[start + 5] * y * y)) >> 10;
        long fixedValue =
            (((c[start + 1] * x) + (c[start + 2] * y) + quadratic) >> 7) + (center * 128L);
        return (float)(fixedValue / 128.0);
    }

    private bool IsValid()
    {
        for (var row = 0; row < Fs50Descriptor.Height; row++)
        {
            for (var column = 0; column < Fs50Descriptor.Width; column++)
            {
                var index = (row * Fs50Descriptor.Width) + column;
                if (
                    !float.IsFinite(mapX[index])
                    || !float.IsFinite(mapY[index])
                    || mapX[index] < 0
                    || mapX[index] >= 1279
                    || mapY[index] < 0
                    || mapY[index] >= 831
                )
                {
                    return false;
                }

                double horizontalScale = Gradient(mapX, column, row, true);
                double verticalScale = Gradient(mapY, column, row, false);
                double determinant =
                    (horizontalScale * verticalScale)
                    - (Gradient(mapX, column, row, false) * Gradient(mapY, column, row, true));
                if (
                    horizontalScale <= 0
                    || verticalScale <= 0
                    || determinant < 0.5
                    || determinant > 4
                )
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static double Gradient(float[] map, int x, int y, bool horizontal)
    {
        var beforeX = horizontal ? Math.Max(0, x - 1) : x;
        var afterX = horizontal ? Math.Min(Fs50Descriptor.Width - 1, x + 1) : x;
        var beforeY = horizontal ? y : Math.Max(0, y - 1);
        var afterY = horizontal ? y : Math.Min(Fs50Descriptor.Height - 1, y + 1);
        return (
                map[(afterY * Fs50Descriptor.Width) + afterX]
                - (double)map[(beforeY * Fs50Descriptor.Width) + beforeX]
            ) / (horizontal ? afterX - beforeX : afterY - beforeY);
    }
}
