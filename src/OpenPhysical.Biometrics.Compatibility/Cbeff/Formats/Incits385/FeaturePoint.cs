namespace OpenPhysical.Domains.Cbeff.Formats.Incits385;

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
/// Represents a facial feature point in an INCITS 385 record.
/// </summary>
public sealed record FeaturePoint
{
    /// <summary>
    /// Gets the feature type.
    /// </summary>
    public byte FeatureType { get; }

    /// <summary>
    /// Gets the feature point identifier.
    /// </summary>
    public byte FeaturePointId { get; }

    /// <summary>
    /// Gets the horizontal position (X coordinate).
    /// </summary>
    public ushort HorizontalPosition { get; }

    /// <summary>
    /// Gets the vertical position (Y coordinate).
    /// </summary>
    public ushort VerticalPosition { get; }

    /// <summary>
    /// Gets the reserved value (2 bytes).
    /// </summary>
    public ushort Reserved { get; }

    private FeaturePoint(
        byte featureType,
        byte featurePointId,
        ushort horizontalPosition,
        ushort verticalPosition,
        ushort reserved
    )
    {
        FeatureType = featureType;
        FeaturePointId = featurePointId;
        HorizontalPosition = horizontalPosition;
        VerticalPosition = verticalPosition;
        Reserved = reserved;
    }

    /// <summary>
    /// Creates a new feature point.
    /// </summary>
    public static Result<FeaturePoint> Create(
        byte featureType,
        byte featurePointId,
        ushort horizontalPosition,
        ushort verticalPosition,
        ushort reserved = 0
    ) =>
        Result.Success(
            new FeaturePoint(
                featureType,
                featurePointId,
                horizontalPosition,
                verticalPosition,
                reserved
            )
        );
}
