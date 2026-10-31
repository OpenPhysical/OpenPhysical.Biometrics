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
/// INCITS 378-2004 Delta Point per Section 6.6.3.
/// A Delta is that point on a ridge at or nearest to the point of divergence
/// of two type lines, and located at or directly in front of the point of divergence.
/// </summary>
public sealed record DeltaPoint : IEquatable<DeltaPoint>
{
    /// <summary>
    /// X coordinate of the delta in pixels per Section 6.6.3.7.
    /// "The x coordinate of the delta shall be recorded in the lower fourteen bits."
    /// Range: 0-16383.
    /// </summary>
    public ushort XCoordinate { get; }

    /// <summary>
    /// Y coordinate of the delta in pixels per Section 6.6.3.7.
    /// "The y coordinate shall be placed in the lower fourteen bits."
    /// Range: 0-16383.
    /// </summary>
    public ushort YCoordinate { get; }

    /// <summary>
    /// Delta angles in units of 2 degrees per Section 6.6.3.8.
    /// Present only when DeltaInformationType indicates angle information is included.
    /// "The three angle attributes of the delta shall be recorded in one byte in units of 2 degrees."
    /// Each angle range: 0-179, representing 0-358 degrees.
    /// Note: Delta points have three angles, unlike core points which have one.
    /// </summary>
    public Maybe<ImmutableArray<byte>> Angles { get; }

    private DeltaPoint(ushort xCoordinate, ushort yCoordinate, Maybe<ImmutableArray<byte>> angles)
    {
        XCoordinate = xCoordinate;
        YCoordinate = yCoordinate;
        Angles = angles;
    }

    /// <summary>
    /// Creates a delta point without angle information.
    /// Used when DeltaInformationType is 0x01 (no angle).
    /// Note: Delta type convention is opposite of core type!
    /// </summary>
    /// <param name="xCoordinate">X position in pixels (14 bits).</param>
    /// <param name="yCoordinate">Y position in pixels (14 bits).</param>
    /// <returns>Success with DeltaPoint or Failure with error message.</returns>
    public static Result<DeltaPoint> CreateWithoutAngles(ushort xCoordinate, ushort yCoordinate)
    {
        // Validate coordinates per Section 6.6.3.7
        var coordinateValidation = ValidateCoordinates(xCoordinate, yCoordinate);
        if (coordinateValidation.IsFailure)
        {
            return Result.Failure<DeltaPoint>(coordinateValidation.Error);
        }

        return Result.Success(
            new DeltaPoint(xCoordinate, yCoordinate, Maybe<ImmutableArray<byte>>.None)
        );
    }

    /// <summary>
    /// Creates a delta point with angle information.
    /// Used when DeltaInformationType is 0x00 (has angles).
    /// Note: Delta type convention is opposite of core type!
    /// </summary>
    /// <param name="xCoordinate">X position in pixels (14 bits).</param>
    /// <param name="yCoordinate">Y position in pixels (14 bits).</param>
    /// <param name="angle1">First angle in units of 2 degrees (0-179).</param>
    /// <param name="angle2">Second angle in units of 2 degrees (0-179).</param>
    /// <param name="angle3">Third angle in units of 2 degrees (0-179).</param>
    /// <returns>Success with DeltaPoint or Failure with error message.</returns>
    public static Result<DeltaPoint> CreateWithAngles(
        ushort xCoordinate,
        ushort yCoordinate,
        byte angle1,
        byte angle2,
        byte angle3
    )
    {
        var errors = new List<string>();

        // Validate coordinates per Section 6.6.3.7
        var coordinateValidation = ValidateCoordinates(xCoordinate, yCoordinate);
        if (coordinateValidation.IsFailure)
        {
            errors.Add(coordinateValidation.Error);
        }

        // Validate each angle per Section 6.6.3.8
        // "The value shall be a nonnegative value between 0 and 179, inclusive"
        if (angle1 > 179)
        {
            errors.Add(
                $"Invalid delta angle1: {angle1} (must be 0-179 per INCITS 378-2004 Section 6.6.3.8)"
            );
        }

        if (angle2 > 179)
        {
            errors.Add(
                $"Invalid delta angle2: {angle2} (must be 0-179 per INCITS 378-2004 Section 6.6.3.8)"
            );
        }

        if (angle3 > 179)
        {
            errors.Add(
                $"Invalid delta angle3: {angle3} (must be 0-179 per INCITS 378-2004 Section 6.6.3.8)"
            );
        }

        return errors.Count == 0
            ? Result.Success(
                new DeltaPoint(
                    xCoordinate,
                    yCoordinate,
                    Maybe<ImmutableArray<byte>>.From(ImmutableArray.Create(angle1, angle2, angle3))
                )
            )
            : Result.Failure<DeltaPoint>(string.Join("; ", errors));
    }

    /// <summary>
    /// Gets the angles in degrees if present.
    /// Converts from units of 2 degrees to actual degrees.
    /// </summary>
    /// <returns>Maybe containing array of angles in degrees (0-358) or None if no angles.</returns>
    public Maybe<ImmutableArray<double>> GetAnglesInDegrees() =>
        Angles.Map(angles => angles.Select(a => a * 2.0).ToImmutableArray());

    /// <summary>
    /// Creates a delta point with optional angle information.
    /// Unified factory method that handles both cases.
    /// </summary>
    /// <param name="xCoordinate">X position in pixels (14 bits).</param>
    /// <param name="yCoordinate">Y position in pixels (14 bits).</param>
    /// <param name="angles">Optional three angles in units of 2 degrees.</param>
    /// <returns>Success with DeltaPoint or Failure with error message.</returns>
    public static Result<DeltaPoint> Create(
        ushort xCoordinate,
        ushort yCoordinate,
        ImmutableArray<byte>? angles
    )
    {
        if (angles.HasValue)
        {
            var anglesArray = angles.Value;
            if (anglesArray.Length != 3)
            {
                return Result.Failure<DeltaPoint>(
                    $"Delta points must have exactly 3 angles, got {anglesArray.Length}"
                );
            }

            var errors = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                if (anglesArray[i] > 179)
                {
                    errors.Add(
                        $"Invalid angle {anglesArray[i]} at position {i} (must be 0-179 per INCITS 378-2004 Section 6.6.3.8)"
                    );
                }
            }

            if (errors.Count > 0)
            {
                return Result.Failure<DeltaPoint>(string.Join("; ", errors));
            }

            return CreateWithAngles(
                xCoordinate,
                yCoordinate,
                anglesArray[0],
                anglesArray[1],
                anglesArray[2]
            );
        }

        return CreateWithoutAngles(xCoordinate, yCoordinate);
    }

    /// <summary>Preserves parsed delta fields for subsequent record-level validation.</summary>
    public static DeltaPoint CreateForParsing(
        ushort xCoordinate,
        ushort yCoordinate,
        ImmutableArray<byte>? angles
    ) =>
        new(
            xCoordinate,
            yCoordinate,
            angles.HasValue
                ? Maybe<ImmutableArray<byte>>.From(angles.Value)
                : Maybe<ImmutableArray<byte>>.None
        );

    /// <summary>Compares coordinates and angular values rather than array identities.</summary>
    public bool Equals(DeltaPoint? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (XCoordinate != other.XCoordinate || YCoordinate != other.YCoordinate)
        {
            return false;
        }

        if (Angles.HasValue != other.Angles.HasValue)
        {
            return false;
        }

        if (!Angles.HasValue)
        {
            return true;
        }

        return Angles.Value.SequenceEqual(other.Angles.Value);
    }

    /// <summary>Combines coordinates and angular values into a value-based hash.</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (XCoordinate * 397) ^ YCoordinate;
            if (Angles.HasValue)
            {
                foreach (var angle in Angles.Value)
                {
                    hash = (hash * 31) ^ angle;
                }
            }

            return hash;
        }
    }

    /// <summary>
    /// Validates coordinates are within 14-bit range per Section 6.6.3.7.
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
