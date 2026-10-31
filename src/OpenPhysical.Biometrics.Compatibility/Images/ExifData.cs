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
/// Represents EXIF metadata extracted from an image.
/// </summary>
public sealed record ExifData
{
    /// <summary>
    /// Gets the camera manufacturer.
    /// </summary>
    public Maybe<string> Make { get; }

    /// <summary>
    /// Gets the camera model.
    /// </summary>
    public Maybe<string> Model { get; }

    /// <summary>
    /// Gets the original date/time the photo was taken.
    /// </summary>
    public Maybe<DateTime> DateTimeOriginal { get; }

    /// <summary>
    /// Gets the date/time the file was changed.
    /// </summary>
    public Maybe<DateTime> DateTime { get; }

    /// <summary>
    /// Gets the software used to create/modify the image.
    /// </summary>
    public Maybe<string> Software { get; }

    /// <summary>
    /// Gets the image description.
    /// </summary>
    public Maybe<string> Description { get; }

    /// <summary>
    /// Gets the artist/photographer.
    /// </summary>
    public Maybe<string> Artist { get; }

    /// <summary>
    /// Gets the copyright information.
    /// </summary>
    public Maybe<string> Copyright { get; }

    /// <summary>
    /// Gets the GPS location if available.
    /// </summary>
    public Maybe<GpsCoordinates> GpsLocation { get; }

    /// <summary>
    /// Gets the exposure time in seconds.
    /// </summary>
    public Maybe<double> ExposureTime { get; }

    /// <summary>
    /// Gets the F-number (aperture).
    /// </summary>
    public Maybe<double> FNumber { get; }

    /// <summary>
    /// Gets the ISO speed rating.
    /// </summary>
    public Maybe<int> IsoSpeed { get; }

    /// <summary>
    /// Gets the focal length in millimeters.
    /// </summary>
    public Maybe<double> FocalLength { get; }

    /// <summary>
    /// Gets the image orientation.
    /// </summary>
    public Maybe<int> Orientation { get; }

    /// <summary>
    /// Gets all EXIF tags as a dictionary.
    /// </summary>
    public ImmutableDictionary<ExifTag, object> AllTags { get; }

    private ExifData(
        Maybe<string> make,
        Maybe<string> model,
        Maybe<DateTime> dateTimeOriginal,
        Maybe<DateTime> dateTime,
        Maybe<string> software,
        Maybe<string> description,
        Maybe<string> artist,
        Maybe<string> copyright,
        Maybe<GpsCoordinates> gpsLocation,
        Maybe<double> exposureTime,
        Maybe<double> fNumber,
        Maybe<int> isoSpeed,
        Maybe<double> focalLength,
        Maybe<int> orientation,
        ImmutableDictionary<ExifTag, object> allTags
    )
    {
        Make = make;
        Model = model;
        DateTimeOriginal = dateTimeOriginal;
        DateTime = dateTime;
        Software = software;
        Description = description;
        Artist = artist;
        Copyright = copyright;
        GpsLocation = gpsLocation;
        ExposureTime = exposureTime;
        FNumber = fNumber;
        IsoSpeed = isoSpeed;
        FocalLength = focalLength;
        Orientation = orientation;
        AllTags = allTags ?? ImmutableDictionary<ExifTag, object>.Empty;
    }

    /// <summary>
    /// Creates a new ExifData instance.
    /// </summary>
    public static ExifData Create(
        Maybe<string> make = default,
        Maybe<string> model = default,
        Maybe<DateTime> dateTimeOriginal = default,
        Maybe<DateTime> dateTime = default,
        Maybe<string> software = default,
        Maybe<string> description = default,
        Maybe<string> artist = default,
        Maybe<string> copyright = default,
        Maybe<GpsCoordinates> gpsLocation = default,
        Maybe<double> exposureTime = default,
        Maybe<double> fNumber = default,
        Maybe<int> isoSpeed = default,
        Maybe<double> focalLength = default,
        Maybe<int> orientation = default,
        ImmutableDictionary<ExifTag, object>? allTags = null
    ) =>
        new(
            make,
            model,
            dateTimeOriginal,
            dateTime,
            software,
            description,
            artist,
            copyright,
            gpsLocation,
            exposureTime,
            fNumber,
            isoSpeed,
            focalLength,
            orientation,
            allTags ?? ImmutableDictionary<ExifTag, object>.Empty
        );

    /// <summary>
    /// Creates an empty ExifData instance.
    /// </summary>
    public static ExifData Empty =>
        new(
            Maybe<string>.None,
            Maybe<string>.None,
            Maybe<DateTime>.None,
            Maybe<DateTime>.None,
            Maybe<string>.None,
            Maybe<string>.None,
            Maybe<string>.None,
            Maybe<string>.None,
            Maybe<GpsCoordinates>.None,
            Maybe<double>.None,
            Maybe<double>.None,
            Maybe<int>.None,
            Maybe<double>.None,
            Maybe<int>.None,
            ImmutableDictionary<ExifTag, object>.Empty
        );
}
