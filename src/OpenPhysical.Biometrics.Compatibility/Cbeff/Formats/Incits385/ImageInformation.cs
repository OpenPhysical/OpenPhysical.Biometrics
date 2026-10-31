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
/// Represents the image information block in an INCITS 385 record.
/// </summary>
public sealed record ImageInformation
{
    /// <summary>
    /// Gets the facial image type (should be 1 for full frontal).
    /// </summary>
    public byte FacialImageType { get; }

    /// <summary>
    /// Gets the image data type (0 = JPEG, 1 = JPEG 2000).
    /// </summary>
    public byte ImageDataType { get; }

    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public ushort Width { get; }

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public ushort Height { get; }

    /// <summary>
    /// Gets the image color space (1 = sRGB).
    /// </summary>
    public byte ImageColorSpace { get; }

    /// <summary>
    /// Gets the source type (2 = digital still, 6 = digital video).
    /// </summary>
    public byte SourceType { get; }

    /// <summary>
    /// Gets the device type (vendor-specific).
    /// </summary>
    public ushort DeviceType { get; }

    /// <summary>
    /// Gets the image quality (0-100, 0 = unspecified).
    /// </summary>
    public ushort Quality { get; }

    private ImageInformation(
        byte facialImageType,
        byte imageDataType,
        ushort width,
        ushort height,
        byte imageColorSpace,
        byte sourceType,
        ushort deviceType,
        ushort quality
    )
    {
        FacialImageType = facialImageType;
        ImageDataType = imageDataType;
        Width = width;
        Height = height;
        ImageColorSpace = imageColorSpace;
        SourceType = sourceType;
        DeviceType = deviceType;
        Quality = quality;
    }

    /// <summary>
    /// Creates a new image information block with validation.
    /// </summary>
    public static Result<ImageInformation> Create(
        byte facialImageType,
        byte imageDataType,
        ushort width,
        ushort height,
        byte imageColorSpace,
        byte sourceType,
        ushort deviceType,
        ushort quality
    )
    {
        if (imageDataType > 1)
            return Result.Failure<ImageInformation>(
                $"Invalid image data type: {imageDataType}, must be 0 (JPEG) or 1 (JPEG 2000)"
            );

        if (width == 0 || height == 0)
            return Result.Failure<ImageInformation>("Image dimensions cannot be zero");

        if (quality > 100 && quality != 0xFFFF)
            return Result.Failure<ImageInformation>(
                $"Invalid quality value: {quality}, must be 0-100 or 0xFFFF"
            );

        return Result.Success(
            new ImageInformation(
                facialImageType,
                imageDataType,
                width,
                height,
                imageColorSpace,
                sourceType,
                deviceType,
                quality
            )
        );
    }

    /// <summary>
    /// Gets a human-readable description of the image data type.
    /// </summary>
    public string GetImageDataTypeDescription() =>
        ImageDataType switch
        {
            0 => "JPEG",
            1 => "JPEG 2000",
            _ => "Unknown",
        };
}
