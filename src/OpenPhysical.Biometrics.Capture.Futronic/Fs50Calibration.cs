// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers.Binary;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Validated, copied factory inputs. Factory calibration is not measured physical scale.</summary>
public sealed class Fs50Calibration
{
    private Fs50Calibration(
        ImmutableArray<int> coefficients,
        ImmutableArray<byte> bytes,
        int offsetX,
        int offsetY
    )
    {
        Coefficients = coefficients;
        Bytes = bytes;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>Signed polynomial coefficients in vendor fixed-point units.</summary>
    public ImmutableArray<int> Coefficients { get; }

    /// <summary>Read-only original calibration bytes for provenance hashing.</summary>
    public ImmutableArray<byte> Bytes { get; }

    /// <summary>Horizontal crop offset.</summary>
    public int OffsetX { get; }

    /// <summary>Vertical crop offset.</summary>
    public int OffsetY { get; }

    /// <summary>Checks descriptor, crop and coefficient encodings before building a map.</summary>
    /// <param name="descriptor">The complete sensor descriptor.</param>
    /// <param name="controls">The read-only device controls.</param>
    /// <param name="calibration">The 4352-byte factory calibration.</param>
    /// <returns>A copied calibration or a precise input failure.</returns>
    public static Result<Fs50Calibration, CaptureError> Parse(
        ReadOnlySpan<byte> descriptor,
        ReadOnlySpan<byte> controls,
        ReadOnlySpan<byte> calibration
    )
    {
        var valid = Fs50Descriptor.Validate(descriptor);
        if (valid.IsFailure)
        {
            return Result.Failure<Fs50Calibration, CaptureError>(valid.Error);
        }

        if (
            controls.Length < 7
            || calibration.Length != 4352
            || controls[3] == 0
            || controls[4] is 0 or > 82
        )
        {
            return Result.Failure<Fs50Calibration, CaptureError>(
                new CaptureError(
                    CaptureErrorCodes.CalibrationInvalid,
                    "Invalid FS50 calibration dimensions or crop."
                )
            );
        }

        var parsed = ParseCoefficients(calibration);
        if (parsed.IsFailure)
        {
            return Result.Failure<Fs50Calibration, CaptureError>(parsed.Error);
        }

        return new Fs50Calibration(
            parsed.Value,
            ImmutableArray.CreateRange(calibration.ToArray()),
            controls[3],
            controls[4]
        );
    }

    private static Result<ImmutableArray<int>, CaptureError> ParseCoefficients(
        ReadOnlySpan<byte> calibration
    )
    {
        var coefficients = new int[12];
        for (var i = 0; i < coefficients.Length; i++)
        {
            coefficients[i] = BinaryPrimitives.ReadInt32LittleEndian(
                calibration.Slice(16 + (4 * i), 4)
            );
            if (coefficients[i] is < -1000000 or > 1000000)
            {
                return Result.Failure<ImmutableArray<int>, CaptureError>(
                    new CaptureError(
                        CaptureErrorCodes.CalibrationInvalid,
                        "Invalid FS50 coefficient."
                    )
                );
            }
        }

        return ImmutableArray.CreateRange(coefficients);
    }
}
