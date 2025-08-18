namespace OpenPhysical.Biometrics.Infrastructure.Metadata;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions.Metadata;
using OpenPhysical.Domains.Biometrics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <summary>
/// Extracts metadata from JPEG images using ImageSharp.
/// </summary>
public sealed class JpegMetadataExtractor : IJpegMetadataExtractor
{
    private readonly ILogger<JpegMetadataExtractor> _logger;
    
    public ImmutableHashSet<OpenPhysical.Domains.Images.ImageFormat> SupportedFormats { get; } = 
        ImmutableHashSet.Create(OpenPhysical.Domains.Images.ImageFormat.Jpeg);
    
    public JpegMetadataExtractor(ILogger<JpegMetadataExtractor> logger)
    {
        _logger = logger;
    }
    
    public Result<bool> CanExtract(ImmutableArray<byte> imageData)
    {
        if (imageData.IsDefaultOrEmpty)
            return Result.Success(false);
            
        // Check JPEG magic bytes (FF D8 FF)
        if (imageData.Length < 3)
            return Result.Success(false);
            
        return Result.Success(
            imageData[0] == 0xFF && 
            imageData[1] == 0xD8 && 
            imageData[2] == 0xFF);
    }
    
    public async Task<Result<OpenPhysical.Domains.Images.ImageMetadata>> ExtractAsync(ImmutableArray<byte> imageData)
    {
        return await Task.Run(() => Extract(imageData));
    }
    
    public Result<OpenPhysical.Domains.Images.ImageMetadata> Extract(ImmutableArray<byte> imageData)
    {
        return ExtractJpegMetadata(imageData)
            .Map(jpeg => jpeg.BaseMetadata);
    }
    
    async Task<Result<OpenPhysical.Domains.Images.JpegMetadata>> IJpegMetadataExtractor.ExtractAsync(ImmutableArray<byte> imageData)
    {
        return await Task.Run(() => ((IJpegMetadataExtractor)this).Extract(imageData));
    }
    
    Result<OpenPhysical.Domains.Images.JpegMetadata> IJpegMetadataExtractor.Extract(ImmutableArray<byte> imageData)
    {
        return ExtractJpegMetadata(imageData);
    }
    
    public Result<OpenPhysical.Domains.Images.ExifData> ExtractExif(ImmutableArray<byte> imageData)
    {
        try
        {
            using var image = Image.Load(imageData.ToArray());
            var exifProfile = image.Metadata.ExifProfile;
            
            if (exifProfile == null)
            {
                _logger.LogDebug("No EXIF profile found in JPEG");
                return Result.Success(OpenPhysical.Domains.Images.ExifData.Empty);
            }
            
            return ParseExifProfile(exifProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract EXIF data");
            return Result.Failure<OpenPhysical.Domains.Images.ExifData>($"Failed to extract EXIF data: {ex.Message}");
        }
    }
    
    private Result<OpenPhysical.Domains.Images.JpegMetadata> ExtractJpegMetadata(ImmutableArray<byte> imageData)
    {
        try
        {
            using var image = Image.Load(imageData.ToArray());
            var metadata = image.Metadata;
            var jpegMetadata = metadata.GetJpegMetadata();
            
            // Extract base metadata
            var dimensions = ImageDimensions.Create((ushort)image.Width, (ushort)image.Height);
            if (dimensions.IsFailure)
                return Result.Failure<OpenPhysical.Domains.Images.JpegMetadata>(dimensions.Error);
                
            // Extract EXIF data
            var exifResult = ExtractExif(imageData);
            var exif = exifResult.IsSuccess ? Maybe<OpenPhysical.Domains.Images.ExifData>.From(exifResult.Value) : Maybe<OpenPhysical.Domains.Images.ExifData>.None;
            
            // Determine dates
            var creationDate = exif.Bind(e => e.DateTimeOriginal);
            var modificationDate = exif.Bind(e => e.DateTime);
            
            // Build base metadata
            var baseMetadataResult = OpenPhysical.Domains.Images.ImageMetadata.Create(
                OpenPhysical.Domains.Images.ImageFormat.Jpeg,
                dimensions.Value,
                creationDate,
                modificationDate,
                Maybe<int>.From(image.PixelType.BitsPerPixel),
                Maybe<string>.From(jpegMetadata?.ColorType?.ToString() ?? "Unknown"),
                Maybe<string>.From("JPEG"),
                exif,
                ImmutableDictionary<string, object>.Empty);
                
            if (baseMetadataResult.IsFailure)
                return Result.Failure<OpenPhysical.Domains.Images.JpegMetadata>(baseMetadataResult.Error);
                
            // Extract JPEG-specific metadata
            var quality = jpegMetadata?.Quality != null 
                ? Maybe<int>.From(jpegMetadata.Quality) 
                : Maybe<int>.None;
                
            var isProgressive = false; // Progressive mode info not directly available
            
            return OpenPhysical.Domains.Images.JpegMetadata.Create(
                baseMetadataResult.Value,
                quality,
                Maybe<string>.None, // Subsampling info not directly available in ImageSharp
                isProgressive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract JPEG metadata");
            return Result.Failure<OpenPhysical.Domains.Images.JpegMetadata>($"Failed to extract JPEG metadata: {ex.Message}");
        }
    }
    
    private Result<OpenPhysical.Domains.Images.ExifData> ParseExifProfile(ExifProfile profile)
    {
        try
        {
            var allTags = new Dictionary<OpenPhysical.Domains.Images.ExifTag, object>();
            
            // Extract common EXIF values
            var make = profile.TryGetValue(ExifTag.Make, out var makeValue) 
                ? Maybe<string>.From(makeValue?.GetValue()?.ToString() ?? string.Empty)
                : Maybe<string>.None;
            var model = profile.TryGetValue(ExifTag.Model, out var modelValue)
                ? Maybe<string>.From(modelValue?.GetValue()?.ToString() ?? string.Empty)
                : Maybe<string>.None;
            var software = profile.TryGetValue(ExifTag.Software, out var softwareValue)
                ? Maybe<string>.From(softwareValue?.GetValue()?.ToString() ?? string.Empty)
                : Maybe<string>.None;
            var description = profile.TryGetValue(ExifTag.ImageDescription, out var descValue)
                ? Maybe<string>.From(descValue?.GetValue()?.ToString() ?? string.Empty)
                : Maybe<string>.None;
            var artist = profile.TryGetValue(ExifTag.Artist, out var artistValue)
                ? Maybe<string>.From(artistValue?.GetValue()?.ToString() ?? string.Empty)
                : Maybe<string>.None;
            var copyright = profile.TryGetValue(ExifTag.Copyright, out var copyrightValue)
                ? Maybe<string>.From(copyrightValue?.GetValue()?.ToString() ?? string.Empty)
                : Maybe<string>.None;
            
            // Extract dates
            var dateTimeOriginal = GetExifDateTimeValue(profile, ExifTag.DateTimeOriginal);
            var dateTime = GetExifDateTimeValue(profile, ExifTag.DateTime);
            
            // Extract camera settings
            var exposureTime = GetExifRationalValue(profile, ExifTag.ExposureTime);
            var fNumber = GetExifRationalValue(profile, ExifTag.FNumber);
            var isoSpeed = profile.TryGetValue(ExifTag.ISOSpeedRatings, out var isoValue) && 
                           isoValue?.GetValue() is ushort[] isoValues && isoValues.Length > 0
                ? Maybe<int>.From((int)isoValues[0])
                : Maybe<int>.None;
            var focalLength = GetExifRationalValue(profile, ExifTag.FocalLength);
            var orientation = profile.TryGetValue(ExifTag.Orientation, out var orientValue) && 
                             orientValue?.GetValue() is ushort orientVal
                ? Maybe<int>.From((int)orientVal)
                : Maybe<int>.None;
            
            // Extract GPS data
            var gpsLocation = ExtractGpsLocation(profile);
            
            // Collect all tags
            foreach (var value in profile.Values)
            {
                // Store raw tag values - we'll map to our domain tags later
                // ExifTag in ImageSharp is generic, so we need to handle it differently
                var tagName = value.Tag.ToString();
                if (tagName != null && TryMapExifTagName(tagName, out var domainTag))
                {
                    allTags[domainTag] = value.GetValue() ?? "null";
                }
            }
            
            return Result.Success(OpenPhysical.Domains.Images.ExifData.Create(
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
                allTags.ToImmutableDictionary()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse EXIF profile");
            return Result.Failure<OpenPhysical.Domains.Images.ExifData>($"Failed to parse EXIF profile: {ex.Message}");
        }
    }
    
    private Maybe<double> GetExifRationalValue(ExifProfile profile, ExifTag<Rational> tag)
    {
        if (profile.TryGetValue(tag, out var value) && value?.GetValue() is Rational rational)
            return Maybe<double>.From(rational.ToDouble());
        return Maybe<double>.None;
    }
    
    private Maybe<DateTime> GetExifDateTimeValue(ExifProfile profile, ExifTag<string> tag)
    {
        if (!profile.TryGetValue(tag, out var value) || value?.GetValue() is not string dateStr)
            return Maybe<DateTime>.None;
            
        // EXIF date format: "YYYY:MM:DD HH:MM:SS"
        if (DateTime.TryParseExact(dateStr, "yyyy:MM:dd HH:mm:ss", 
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, 
            out var dateTime))
        {
            return Maybe<DateTime>.From(dateTime);
        }
        
        return Maybe<DateTime>.None;
    }
    
    private Maybe<OpenPhysical.Domains.Images.GpsCoordinates> ExtractGpsLocation(ExifProfile profile)
    {
        var latitudeRef = profile.TryGetValue(ExifTag.GPSLatitudeRef, out var latRefValue)
            ? Maybe<string>.From(latRefValue?.GetValue()?.ToString() ?? string.Empty)
            : Maybe<string>.None;
        profile.TryGetValue(ExifTag.GPSLatitude, out var latitude);
        var longitudeRef = profile.TryGetValue(ExifTag.GPSLongitudeRef, out var lonRefValue)
            ? Maybe<string>.From(lonRefValue?.GetValue()?.ToString() ?? string.Empty)
            : Maybe<string>.None;
        profile.TryGetValue(ExifTag.GPSLongitude, out var longitude);
        
        if (latitude?.GetValue() is Rational[] latValues && latValues.Length >= 3 &&
            longitude?.GetValue() is Rational[] lonValues && lonValues.Length >= 3 &&
            latitudeRef.HasValue && longitudeRef.HasValue)
        {
            profile.TryGetValue(ExifTag.GPSAltitude, out var altitude);
            var altitudeValue = altitude?.GetValue() is Rational altRational
                ? Maybe<double>.From(altRational.ToDouble())
                : Maybe<double>.None;
                
            var result = OpenPhysical.Domains.Images.GpsCoordinates.CreateFromDms(
                latValues[0].ToDouble(),
                latValues[1].ToDouble(),
                latValues[2].ToDouble(),
                latitudeRef.Value,
                lonValues[0].ToDouble(),
                lonValues[1].ToDouble(),
                lonValues[2].ToDouble(),
                longitudeRef.Value,
                altitudeValue);
                
            if (result.IsSuccess)
                return Maybe<OpenPhysical.Domains.Images.GpsCoordinates>.From(result.Value);
        }
        
        return Maybe<OpenPhysical.Domains.Images.GpsCoordinates>.None;
    }
    
    private bool TryMapExifTagName(string tagName, out OpenPhysical.Domains.Images.ExifTag domainTag)
    {
        domainTag = default;
        
        // Try to parse the tag name to our domain enum
        if (Enum.TryParse<OpenPhysical.Domains.Images.ExifTag>(tagName, out var parsedTag))
        {
            domainTag = parsedTag;
            return true;
        }
        
        // Handle special cases where names might not match exactly
        return tagName switch
        {
            "ISOSpeedRatings" => TrySetTag(OpenPhysical.Domains.Images.ExifTag.ISOSpeedRatings, out domainTag),
            "GPSLatitudeRef" => TrySetTag(OpenPhysical.Domains.Images.ExifTag.GPSLatitudeRef, out domainTag),
            "GPSLongitudeRef" => TrySetTag(OpenPhysical.Domains.Images.ExifTag.GPSLongitudeRef, out domainTag),
            "GPSLatitude" => TrySetTag(OpenPhysical.Domains.Images.ExifTag.GPSLatitude, out domainTag),
            "GPSLongitude" => TrySetTag(OpenPhysical.Domains.Images.ExifTag.GPSLongitude, out domainTag),
            "GPSAltitude" => TrySetTag(OpenPhysical.Domains.Images.ExifTag.GPSAltitude, out domainTag),
            _ => false
        };
        
        bool TrySetTag(OpenPhysical.Domains.Images.ExifTag tag, out OpenPhysical.Domains.Images.ExifTag result)
        {
            result = tag;
            return true;
        }
    }
}