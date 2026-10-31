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
using OpenPhysical.Domains.Biometrics;

/// <summary>
/// Represents metadata extracted from an image file.
/// </summary>
public sealed record ImageMetadata
{
    /// <summary>
    /// Gets the image format.
    /// </summary>
    public ImageFormat Format { get; }

    /// <summary>
    /// Gets the image dimensions.
    /// </summary>
    public ImageDimensions Dimensions { get; }

    /// <summary>
    /// Gets the creation date of the image.
    /// </summary>
    public Maybe<DateTime> CreationDate { get; }

    /// <summary>
    /// Gets the modification date of the image.
    /// </summary>
    public Maybe<DateTime> ModificationDate { get; }

    /// <summary>
    /// Gets the color depth in bits per pixel.
    /// </summary>
    public Maybe<int> ColorDepth { get; }

    /// <summary>
    /// Gets the color space (e.g., RGB, CMYK, Grayscale).
    /// </summary>
    public Maybe<string> ColorSpace { get; }

    /// <summary>
    /// Gets the compression type if applicable.
    /// </summary>
    public Maybe<string> Compression { get; }

    /// <summary>
    /// Gets the EXIF data if available (primarily for JPEG).
    /// </summary>
    public Maybe<ExifData> Exif { get; }

    /// <summary>
    /// Gets custom metadata tags.
    /// </summary>
    public ImmutableDictionary<string, object> CustomTags { get; }

    private ImageMetadata(
        ImageFormat format,
        ImageDimensions dimensions,
        Maybe<DateTime> creationDate,
        Maybe<DateTime> modificationDate,
        Maybe<int> colorDepth,
        Maybe<string> colorSpace,
        Maybe<string> compression,
        Maybe<ExifData> exif,
        ImmutableDictionary<string, object> customTags
    )
    {
        Format = format;
        Dimensions = dimensions;
        CreationDate = creationDate;
        ModificationDate = modificationDate;
        ColorDepth = colorDepth;
        ColorSpace = colorSpace;
        Compression = compression;
        Exif = exif;
        CustomTags = customTags ?? ImmutableDictionary<string, object>.Empty;
    }

    /// <summary>
    /// Creates a new ImageMetadata instance.
    /// </summary>
    public static Result<ImageMetadata> Create(
        ImageFormat format,
        ImageDimensions dimensions,
        Maybe<DateTime> creationDate = default,
        Maybe<DateTime> modificationDate = default,
        Maybe<int> colorDepth = default,
        Maybe<string> colorSpace = default,
        Maybe<string> compression = default,
        Maybe<ExifData> exif = default,
        ImmutableDictionary<string, object>? customTags = null
    )
    {
        if (format == ImageFormat.Unknown)
            return Result.Failure<ImageMetadata>("Image format cannot be unknown");

        return Result.Success(
            new ImageMetadata(
                format,
                dimensions,
                creationDate,
                modificationDate,
                colorDepth,
                colorSpace,
                compression,
                exif,
                customTags ?? ImmutableDictionary<string, object>.Empty
            )
        );
    }
}
