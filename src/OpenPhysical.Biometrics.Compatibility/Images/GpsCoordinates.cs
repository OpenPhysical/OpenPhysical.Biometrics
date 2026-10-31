namespace OpenPhysical.Domains.Images;

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
/// Represents GPS coordinates from EXIF data.
/// </summary>
public sealed record GpsCoordinates
{
    /// <summary>
    /// Gets the latitude in decimal degrees.
    /// Positive values indicate North, negative values indicate South.
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// Gets the longitude in decimal degrees.
    /// Positive values indicate East, negative values indicate West.
    /// </summary>
    public double Longitude { get; }

    /// <summary>
    /// Gets the altitude in meters.
    /// </summary>
    public Maybe<double> Altitude { get; }

    private GpsCoordinates(double latitude, double longitude, Maybe<double> altitude)
    {
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
    }

    /// <summary>
    /// Creates GPS coordinates with validation.
    /// </summary>
    public static Result<GpsCoordinates> Create(
        double latitude,
        double longitude,
        Maybe<double> altitude = default
    )
    {
        if (latitude < -90 || latitude > 90)
            return Result.Failure<GpsCoordinates>(
                $"Invalid latitude: {latitude}. Must be between -90 and 90."
            );

        if (longitude < -180 || longitude > 180)
            return Result.Failure<GpsCoordinates>(
                $"Invalid longitude: {longitude}. Must be between -180 and 180."
            );

        return Result.Success(new GpsCoordinates(latitude, longitude, altitude));
    }

    /// <summary>
    /// Creates GPS coordinates from degrees, minutes, and seconds.
    /// </summary>
    public static Result<GpsCoordinates> CreateFromDms(
        double latDegrees,
        double latMinutes,
        double latSeconds,
        string latRef,
        double lonDegrees,
        double lonMinutes,
        double lonSeconds,
        string lonRef,
        Maybe<double> altitude = default
    )
    {
        var latitude = ConvertDmsToDecimal(latDegrees, latMinutes, latSeconds);
        if (latRef == "S")
            latitude = -latitude;

        var longitude = ConvertDmsToDecimal(lonDegrees, lonMinutes, lonSeconds);
        if (lonRef == "W")
            longitude = -longitude;

        return Create(latitude, longitude, altitude);
    }

    private static double ConvertDmsToDecimal(double degrees, double minutes, double seconds) =>
        degrees + (minutes / 60.0) + (seconds / 3600.0);

    /// <summary>Formats the recorded latitude, longitude, and optional altitude.</summary>
    public override string ToString()
    {
        var alt = Altitude.HasValue ? $", {Altitude.Value:F2}m" : "";
        return $"{Math.Abs(Latitude):F6}°{(Latitude >= 0 ? "N" : "S")}, {Math.Abs(Longitude):F6}°{(Longitude >= 0 ? "E" : "W")}{alt}";
    }
}
