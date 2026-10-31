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
/// Represents a complete INCITS 385 facial image record.
/// </summary>
public sealed record FacialImageRecord
{
    /// <summary>
    /// Gets the facial record header.
    /// </summary>
    public FacialRecordHeader Header { get; }

    /// <summary>
    /// Gets the facial information block.
    /// </summary>
    public FacialInformation FacialInfo { get; }

    /// <summary>
    /// Gets the feature points (may be empty).
    /// </summary>
    public ImmutableArray<FeaturePoint> FeaturePoints { get; }

    /// <summary>
    /// Gets the image information block.
    /// </summary>
    public ImageInformation ImageInfo { get; }

    /// <summary>
    /// Gets the raw image data.
    /// </summary>
    public ImmutableArray<byte> ImageData { get; }

    private FacialImageRecord(
        FacialRecordHeader header,
        FacialInformation facialInfo,
        ImmutableArray<FeaturePoint> featurePoints,
        ImageInformation imageInfo,
        ImmutableArray<byte> imageData
    )
    {
        Header = header;
        FacialInfo = facialInfo;
        FeaturePoints = featurePoints;
        ImageInfo = imageInfo;
        ImageData = imageData;
    }

    /// <summary>
    /// Creates a new facial image record with validation.
    /// </summary>
    public static Result<FacialImageRecord> Create(
        FacialRecordHeader header,
        FacialInformation facialInfo,
        ImmutableArray<FeaturePoint> featurePoints,
        ImageInformation imageInfo,
        ImmutableArray<byte> imageData
    )
    {
        if (header == null)
            return Result.Failure<FacialImageRecord>("Header cannot be null");

        if (facialInfo == null)
            return Result.Failure<FacialImageRecord>("Facial information cannot be null");

        if (imageInfo == null)
            return Result.Failure<FacialImageRecord>("Image information cannot be null");

        if (imageData.IsDefaultOrEmpty)
            return Result.Failure<FacialImageRecord>("Image data cannot be empty");

        if (facialInfo.NumberOfFeaturePoints != featurePoints.Length)
            return Result.Failure<FacialImageRecord>(
                $"Feature point count mismatch: expected {facialInfo.NumberOfFeaturePoints}, got {featurePoints.Length}"
            );

        return Result.Success(
            new FacialImageRecord(header, facialInfo, featurePoints, imageInfo, imageData)
        );
    }

    /// <summary>
    /// Checks if the image data appears to be the expected format based on the image info.
    /// </summary>
    public Result<bool> ValidateImageFormat()
    {
        if (ImageData.Length < 4)
            return Result.Failure<bool>("Image data too small to validate format");

        return ImageInfo.ImageDataType switch
        {
            0 => ValidateJpegFormat(),
            1 => ValidateJpeg2000Format(),
            _ => Result.Failure<bool>($"Unknown image data type: {ImageInfo.ImageDataType}"),
        };
    }

    private Result<bool> ValidateJpegFormat()
    {
        if (ImageData.Length >= 2 && ImageData[0] == 0xFF && ImageData[1] == 0xD8)
            return Result.Success(true);

        return Result.Failure<bool>("Image data does not have JPEG signature");
    }

    private Result<bool> ValidateJpeg2000Format()
    {
        // Check for JP2 format
        if (ImageData.Length >= 12)
        {
            var jp2Signature = new byte[]
            {
                0x00,
                0x00,
                0x00,
                0x0c,
                0x6a,
                0x50,
                0x20,
                0x20,
                0x0d,
                0x0a,
                0x87,
                0x0a,
            };
            if (ImageData.Take(12).SequenceEqual(jp2Signature))
                return Result.Success(true);
        }

        // Check for J2C codestream format
        if (ImageData.Length >= 4)
        {
            if (
                ImageData[0] == 0xFF
                && ImageData[1] == 0x4F
                && ImageData[2] == 0xFF
                && ImageData[3] == 0x51
            )
                return Result.Success(true);
        }

        return Result.Failure<bool>("Image data does not have JPEG 2000 signature");
    }
}
