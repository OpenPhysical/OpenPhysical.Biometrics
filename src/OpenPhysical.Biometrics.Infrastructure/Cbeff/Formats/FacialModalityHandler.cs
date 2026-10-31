namespace OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Core.Domain;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;

/// <summary>
/// Modality handler for facial biometric data processing and extraction.
/// </summary>
public sealed class FacialModalityHandler
    : IBiometricModalityHandler<FacialImageRecord, ExtractedBiometricData>
{
    /// <inheritdoc />
    public string ModalityName => "Face";

    /// <inheritdoc />
    public bool CanHandle(FacialImageRecord record)
    {
        return record != null;
    }

    /// <inheritdoc />
    public Result<ExtractedBiometricData> ExtractBiometricData(FacialImageRecord record)
    {
        var extension = GetImageExtension(record.ImageInfo.ImageDataType);

        var metadata = CreateMetadata(record);

        var facialData = new FacialImageData(
            record.ImageData,
            "INCITS 385",
            extension,
            (int)record.ImageInfo.Width,
            (int)record.ImageInfo.Height,
            (byte)record.ImageInfo.Quality,
            metadata
        );

        return Result.Success<ExtractedBiometricData>(facialData);
    }

    /// <inheritdoc />
    public Result<string> AnalyzeBiometricData(FacialImageRecord record)
    {
        var analysis = new
        {
            FormatIdentifier = record.Header.FormatIdentifier.TrimEnd('\0'),
            VersionNumber = record.Header.VersionNumber.TrimEnd('\0'),
            RecordLength = record.Header.RecordLength,
            Gender = GetGenderName(record.FacialInfo.Gender),
            EyeColor = GetEyeColorName(record.FacialInfo.EyeColor),
            HairColor = GetHairColorName(record.FacialInfo.HairColor),
            Expression = record.FacialInfo.Expression,
            FeaturePointCount = record.FeaturePoints.Length,
            ImageInfo = new
            {
                Type = GetImageTypeName(record.ImageInfo.FacialImageType),
                Format = GetFormatName(record.ImageInfo.ImageDataType),
                Width = record.ImageInfo.Width,
                Height = record.ImageInfo.Height,
                ColorSpace = GetColorSpaceName(record.ImageInfo.ImageColorSpace),
                SourceType = GetSourceTypeName(record.ImageInfo.SourceType),
                Quality = record.ImageInfo.Quality,
                SizeBytes = record.ImageData.Length,
            },
        };

        var json = JsonSerializer.Serialize(
            analysis,
            new JsonSerializerOptions { WriteIndented = true }
        );

        return Result.Success(json);
    }

    /// <inheritdoc />
    public Result ValidateModalityRequirements(FacialImageRecord record)
    {
        var errors = new List<string>();

        // Validate image dimensions (PIV requirements)
        if (record.ImageInfo.Width < 240 || record.ImageInfo.Height < 240)
        {
            errors.Add(
                $"Image dimensions {record.ImageInfo.Width}×{record.ImageInfo.Height} below PIV minimum 240×240"
            );
        }

        // Validate quality score
        if (record.ImageInfo.Quality == 0)
        {
            errors.Add("Image quality score is 0 (undefined)");
        }

        // Validate image data presence
        if (record.ImageData.IsDefaultOrEmpty)
        {
            errors.Add("No image data present");
        }

        // Validate image format
        if (record.ImageInfo.ImageDataType != 0 && record.ImageInfo.ImageDataType != 1)
        {
            errors.Add($"Unsupported image format: {record.ImageInfo.ImageDataType}");
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }

    private static ImmutableDictionary<string, object> CreateMetadata(FacialImageRecord record)
    {
        return ImmutableDictionary<string, object>
            .Empty.Add("FormatIdentifier", record.Header.FormatIdentifier.TrimEnd('\0'))
            .Add("VersionNumber", record.Header.VersionNumber.TrimEnd('\0'))
            .Add("RecordLength", record.Header.RecordLength)
            .Add("Gender", GetGenderName(record.FacialInfo.Gender))
            .Add("EyeColor", GetEyeColorName(record.FacialInfo.EyeColor))
            .Add("HairColor", GetHairColorName(record.FacialInfo.HairColor))
            .Add("Expression", record.FacialInfo.Expression)
            .Add("FeaturePointCount", record.FeaturePoints.Length)
            .Add("ImageType", GetImageTypeName(record.ImageInfo.FacialImageType))
            .Add("ColorSpace", GetColorSpaceName(record.ImageInfo.ImageColorSpace))
            .Add("SourceType", GetSourceTypeName(record.ImageInfo.SourceType))
            .Add("DeviceType", record.ImageInfo.DeviceType)
            .Add("SizeBytes", record.ImageData.Length);
    }

    private static string GetImageExtension(byte imageDataType) =>
        imageDataType switch
        {
            0 => ".jpg",
            1 => ".jp2",
            _ => ".bin",
        };

    private static string GetFormatName(byte type) =>
        type switch
        {
            0 => "JPEG",
            1 => "JPEG 2000",
            _ => $"Unknown ({type})",
        };

    private static string GetImageTypeName(byte type) =>
        type switch
        {
            0x01 => "Full frontal",
            0x02 => "Token frontal",
            _ => $"Unknown ({type})",
        };

    private static string GetColorSpaceName(byte colorSpace) =>
        colorSpace switch
        {
            0x00 => "Unspecified",
            0x01 => "24-bit RGB",
            0x02 => "YUV422",
            0x03 => "8-bit grayscale",
            0x04 => "Other",
            _ => $"Unknown ({colorSpace})",
        };

    private static string GetSourceTypeName(byte sourceType) =>
        sourceType switch
        {
            0x00 => "Unspecified",
            0x01 => "Static photograph - unknown source",
            0x02 => "Static photograph - digital camera",
            0x03 => "Static photograph - scanner",
            0x04 => "Video frame - unknown source",
            0x05 => "Video frame - analog camera",
            0x06 => "Video frame - digital camera",
            0x07 => "Unknown",
            _ => $"Invalid ({sourceType})",
        };

    private static string GetGenderName(byte gender) =>
        gender switch
        {
            0x00 => "Unspecified",
            0x01 => "Male",
            0x02 => "Female",
            0xFF => "Unknown",
            _ => $"Invalid ({gender})",
        };

    private static string GetEyeColorName(byte color) =>
        color switch
        {
            0x00 => "Unspecified",
            0x01 => "Blue",
            0x02 => "Brown",
            0x03 => "Gray",
            0x04 => "Green",
            0x05 => "Hazel",
            0x06 => "Maroon",
            0x07 => "Pink",
            0x08 => "Multi-colored",
            0xFF => "Unknown",
            _ => $"Invalid ({color})",
        };

    private static string GetHairColorName(byte color) =>
        color switch
        {
            0x00 => "Unspecified",
            0x01 => "Bald",
            0x02 => "Black",
            0x03 => "Blonde",
            0x04 => "Brown",
            0x05 => "Gray",
            0x06 => "Red",
            0x07 => "Blue",
            0x08 => "Green",
            0xFF => "Unknown",
            _ => $"Invalid ({color})",
        };
}
