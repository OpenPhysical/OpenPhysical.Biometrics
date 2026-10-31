namespace OpenPhysical.Domains.Cbeff.Formats.Incits378;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;

/// <summary>
/// INCITS 378-2004 Core Point per Section 6.6.3.
/// Represents the approximate center of a fingerprint pattern.
/// Core points are determined by the overall pattern of ridges in the fingerprint.
/// </summary>
public sealed record CorePoint
{
    /// <summary>
    /// X coordinate of the core in pixels per Section 6.6.3.3.
    /// "The x coordinate of the core shall be recorded in the lower fourteen bits."
    /// Range: 0-16383.
    /// </summary>
    public ushort XCoordinate { get; }

    /// <summary>
    /// Y coordinate of the core in pixels per Section 6.6.3.3.
    /// "The y coordinate shall be placed in the lower fourteen bits."
    /// Range: 0-16383.
    /// </summary>
    public ushort YCoordinate { get; }

    /// <summary>
    /// Core angle in units of 2 degrees per Section 6.6.3.4.
    /// Present only when CoreInformationType indicates angle information is included.
    /// "The angle of the core shall be recorded in one byte in units of 2 degrees."
    /// Range: 0-179, representing 0-358 degrees.
    /// </summary>
    public Maybe<byte> Angle { get; }

    private CorePoint(ushort xCoordinate, ushort yCoordinate, Maybe<byte> angle)
    {
        XCoordinate = xCoordinate;
        YCoordinate = yCoordinate;
        Angle = angle;
    }

    /// <summary>
    /// Creates a core point without angle information.
    /// Used when CoreInformationType is 0x00 (no angle).
    /// </summary>
    /// <param name="xCoordinate">X position in pixels (14 bits).</param>
    /// <param name="yCoordinate">Y position in pixels (14 bits).</param>
    /// <returns>Success with CorePoint or Failure with error message.</returns>
    public static Result<CorePoint> CreateWithoutAngle(ushort xCoordinate, ushort yCoordinate)
    {
        // Validate coordinates per Section 6.6.3.3
        var coordinateValidation = ValidateCoordinates(xCoordinate, yCoordinate);
        if (coordinateValidation.IsFailure)
        {
            return Result.Failure<CorePoint>(coordinateValidation.Error);
        }

        return Result.Success(new CorePoint(xCoordinate, yCoordinate, Maybe<byte>.None));
    }

    /// <summary>
    /// Creates a core point with angle information.
    /// Used when CoreInformationType is 0x01 (has angle).
    /// </summary>
    /// <param name="xCoordinate">X position in pixels (14 bits).</param>
    /// <param name="yCoordinate">Y position in pixels (14 bits).</param>
    /// <param name="angle">Angle in units of 2 degrees (0-179).</param>
    /// <returns>Success with CorePoint or Failure with error message.</returns>
    public static Result<CorePoint> CreateWithAngle(
        ushort xCoordinate,
        ushort yCoordinate,
        byte angle
    )
    {
        var errors = new List<string>();

        // Validate coordinates per Section 6.6.3.3
        var coordinateValidation = ValidateCoordinates(xCoordinate, yCoordinate);
        if (coordinateValidation.IsFailure)
        {
            errors.Add(coordinateValidation.Error);
        }

        // Validate angle per Section 6.6.3.4
        // "The value shall be a nonnegative value between 0 and 179, inclusive"
        if (angle > 179)
        {
            errors.Add(
                $"Invalid angle: {angle} (must be 0-179 per INCITS 378-2004 Section 6.6.3.4)"
            );
        }

        return errors.Count == 0
            ? Result.Success(new CorePoint(xCoordinate, yCoordinate, Maybe<byte>.From(angle)))
            : Result.Failure<CorePoint>(string.Join("; ", errors));
    }

    /// <summary>
    /// Gets the angle in degrees if present.
    /// Converts from units of 2 degrees to actual degrees.
    /// </summary>
    /// <returns>Angle in degrees (0-358) or null if no angle.</returns>
    public double? GetAngleInDegrees() => Angle.HasValue ? Angle.Value * 2.0 : null;

    /// <summary>Creates a validated core point with an optional two-degree angle.</summary>
    public static Result<CorePoint> Create(ushort xCoordinate, ushort yCoordinate, byte? angle) =>
        angle.HasValue
            ? CreateWithAngle(xCoordinate, yCoordinate, angle.Value)
            : CreateWithoutAngle(xCoordinate, yCoordinate);

    /// <summary>Preserves parsed core fields for subsequent record-level validation.</summary>
    public static CorePoint CreateForParsing(ushort xCoordinate, ushort yCoordinate, byte? angle) =>
        new(
            xCoordinate,
            yCoordinate,
            angle.HasValue ? Maybe<byte>.From(angle.Value) : Maybe<byte>.None
        );

    /// <summary>
    /// Validates coordinates are within 14-bit range per Section 6.6.3.3.
    /// </summary>
    private static Result ValidateCoordinates(ushort xCoordinate, ushort yCoordinate)
    {
        var errors = new List<string>();

        // Validate X coordinate (14 bits max)
        if (xCoordinate > 16383)
        {
            errors.Add($"Invalid X coordinate: {xCoordinate} (must be 0-16383, 14 bits)");
        }

        // Validate Y coordinate (14 bits max)
        if (yCoordinate > 16383)
        {
            errors.Add($"Invalid Y coordinate: {yCoordinate} (must be 0-16383, 14 bits)");
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }
}
