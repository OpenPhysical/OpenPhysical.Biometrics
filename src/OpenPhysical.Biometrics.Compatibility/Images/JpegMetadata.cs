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
/// Represents JPEG-specific metadata.
/// </summary>
public sealed record JpegMetadata
{
    /// <summary>
    /// Gets the base image metadata.
    /// </summary>
    public ImageMetadata BaseMetadata { get; }

    /// <summary>
    /// Gets the JPEG quality factor (0-100).
    /// </summary>
    public Maybe<int> Quality { get; }

    /// <summary>
    /// Gets the JPEG subsampling information.
    /// </summary>
    public Maybe<string> Subsampling { get; }

    /// <summary>
    /// Gets whether the JPEG is progressive.
    /// </summary>
    public bool IsProgressive { get; }

    // Convenience properties from base metadata
    /// <summary>Gets the encoded image format from the base metadata.</summary>
    public ImageFormat Format => BaseMetadata.Format;

    /// <summary>Gets the image dimensions from the base metadata.</summary>
    public ImageDimensions Dimensions => BaseMetadata.Dimensions;

    /// <summary>Gets the recorded image creation timestamp, when available.</summary>
    public Maybe<DateTime> CreationDate => BaseMetadata.CreationDate;

    /// <summary>Gets the recorded modification timestamp, when available.</summary>
    public Maybe<DateTime> ModificationDate => BaseMetadata.ModificationDate;

    /// <summary>Gets the recorded colour depth, when available.</summary>
    public Maybe<int> ColorDepth => BaseMetadata.ColorDepth;

    /// <summary>Gets the recorded colour-space name, when available.</summary>
    public Maybe<string> ColorSpace => BaseMetadata.ColorSpace;

    /// <summary>Gets the recorded compression description, when available.</summary>
    public Maybe<string> Compression => BaseMetadata.Compression;

    /// <summary>Gets the parsed EXIF metadata, when available.</summary>
    public Maybe<ExifData> Exif => BaseMetadata.Exif;

    /// <summary>Gets additional immutable metadata tags.</summary>
    public ImmutableDictionary<string, object> CustomTags => BaseMetadata.CustomTags;

    private JpegMetadata(
        ImageMetadata baseMetadata,
        Maybe<int> quality,
        Maybe<string> subsampling,
        bool isProgressive
    )
    {
        BaseMetadata = baseMetadata;
        Quality = quality;
        Subsampling = subsampling;
        IsProgressive = isProgressive;
    }

    /// <summary>
    /// Creates a new JpegMetadata instance.
    /// </summary>
    public static Result<JpegMetadata> Create(
        ImageMetadata baseMetadata,
        Maybe<int> quality = default,
        Maybe<string> subsampling = default,
        bool isProgressive = false
    )
    {
        if (baseMetadata.Format != ImageFormat.Jpeg)
            return Result.Failure<JpegMetadata>("Base metadata must be for JPEG format");

        if (quality.HasValue && (quality.Value < 0 || quality.Value > 100))
            return Result.Failure<JpegMetadata>("JPEG quality must be between 0 and 100");

        return Result.Success(new JpegMetadata(baseMetadata, quality, subsampling, isProgressive));
    }
}
