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
/// INCITS 378 Minutia Point per Table 8 of INCITS 378-2004.
/// Represents a single fingerprint minutia with position, type, angle, and quality.
/// </summary>
public sealed record MinutiaPoint
{
    /// <summary>
    /// Minutia type (2 bits).
    /// 0 = ridge ending, 1 = ridge bifurcation, 2-3 = other types.
    /// </summary>
    public MinutiaType MinutiaType { get; }

    /// <summary>
    /// X coordinate in units defined by image resolution (14 bits).
    /// Range: 0-16383.
    /// </summary>
    public ushort XCoordinate { get; }

    /// <summary>
    /// Y coordinate in units defined by image resolution (14 bits).
    /// Range: 0-16383.
    /// </summary>
    public ushort YCoordinate { get; }

    /// <summary>
    /// Minutia angle in units of 2 degrees per INCITS 378-2004 Section 6.5.2.3.
    /// "The angle of the minutia shall be recorded in one byte in units of 2 degrees."
    /// Range: 0-179, representing 0-358 degrees.
    /// Example: an angle value of 16 represents 32 degrees.
    /// </summary>
    public byte Angle { get; }

    /// <summary>
    /// Minutia quality (1 byte).
    /// Range: 0-100, where 0 = quality not available.
    /// </summary>
    public byte Quality { get; }

    private MinutiaPoint(
        MinutiaType minutiaType,
        ushort xCoordinate,
        ushort yCoordinate,
        byte angle,
        byte quality
    )
    {
        MinutiaType = minutiaType;
        XCoordinate = xCoordinate;
        YCoordinate = yCoordinate;
        Angle = angle;
        Quality = quality;
    }

    /// <summary>
    /// Creates a new MinutiaPoint with validation.
    /// </summary>
    public static Result<MinutiaPoint> Create(
        MinutiaType minutiaType,
        ushort xCoordinate,
        ushort yCoordinate,
        byte angle,
        byte quality
    )
    {
        var errors = new List<string>();

        // Validate minutia type (only 2 bits used)
        if (!Enum.IsDefined(minutiaType))
        {
            errors.Add($"Invalid minutia type: {minutiaType} (must be 0-2)");
        }

        // Validate coordinates (only 14 bits used)
        if (xCoordinate > 16383)
        {
            errors.Add($"Invalid X coordinate: {xCoordinate} (must be 0-16383)");
        }

        if (yCoordinate > 16383)
        {
            errors.Add($"Invalid Y coordinate: {yCoordinate} (must be 0-16383)");
        }

        // Validate angle per INCITS 378-2004 Section 6.5.2.3
        // "The value shall be a nonnegative value between 0 and 179, inclusive"
        if (angle > 179)
        {
            errors.Add(
                $"Invalid angle: {angle} (must be 0-179 per INCITS 378-2004 Section 6.5.2.3)"
            );
        }

        // Validate quality
        if (quality > 100)
        {
            errors.Add($"Invalid quality: {quality} (must be 0-100)");
        }

        return errors.Count == 0
            ? Result.Success(
                new MinutiaPoint(minutiaType, xCoordinate, yCoordinate, angle, quality)
            )
            : Result.Failure<MinutiaPoint>(string.Join("; ", errors));
    }

    /// <summary>Creates a validated minutia from its raw two-bit type value.</summary>
    public static Result<MinutiaPoint> Create(
        byte minutiaType,
        ushort xCoordinate,
        ushort yCoordinate,
        byte angle,
        byte quality
    ) => Create((MinutiaType)minutiaType, xCoordinate, yCoordinate, angle, quality);

    /// <summary>
    /// Gets the minutia type name for display.
    /// </summary>
    public string GetMinutiaTypeName() =>
        MinutiaType switch
        {
            MinutiaType.RidgeEnding => "Ridge ending",
            MinutiaType.Bifurcation => "Ridge bifurcation",
            MinutiaType.Other => "Other",
            _ => $"Invalid ({MinutiaType})",
        };

    /// <summary>
    /// Gets the angle in degrees (0-358) per INCITS 378-2004 Section 6.5.2.3.
    /// The angle is stored as units of 2 degrees, so conversion is angle * 2.
    /// "The angle of the minutia shall be recorded in one byte in units of 2 degrees."
    /// </summary>
    /// <returns>The angle in degrees, range 0-358.</returns>
    public double GetAngleInDegrees() => Angle * 2.0;

    /// <summary>
    /// Validates the minutia point for typical quality requirements.
    /// </summary>
    public Result ValidateQuality()
    {
        var errors = new List<string>();

        // Check for zero coordinates (unusual but not invalid)
        if (XCoordinate == 0 && YCoordinate == 0)
            errors.Add("Minutia at origin (0,0) is unusual");

        // Check quality
        if (Quality == 0)
            errors.Add("Quality not available (0)");
        else if (Quality < 50)
            errors.Add($"Low quality score: {Quality}");

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }

    /// <summary>
    /// Calculates the distance to another minutia point.
    /// </summary>
    public double DistanceTo(MinutiaPoint other)
    {
        var dx = XCoordinate - other.XCoordinate;
        var dy = YCoordinate - other.YCoordinate;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Calculates the angle difference to another minutia point in degrees.
    /// Returns the minimum angle difference (0-180 degrees).
    /// Note: Maximum angle is 358 degrees per INCITS 378-2004 Section 6.5.2.3.
    /// </summary>
    public double AngleDifferenceTo(MinutiaPoint other)
    {
        var thisAngle = GetAngleInDegrees();
        var otherAngle = other.GetAngleInDegrees();

        var diff = Math.Abs(thisAngle - otherAngle);
        // Even though max angle is 358, we still use 360 for circular difference
        // since 358 degrees is effectively -2 degrees from 0
        return Math.Min(diff, 360.0 - diff);
    }
}
