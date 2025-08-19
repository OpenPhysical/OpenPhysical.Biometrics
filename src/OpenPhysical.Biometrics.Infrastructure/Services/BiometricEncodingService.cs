using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// Pure encoding service that encodes images to various formats without any transformations.
/// This service focuses solely on format conversion and compression.
/// </summary>
[PublicAPI]
public interface IBiometricEncodingService
{
    /// <summary>
    /// Encodes an image to JPEG 2000 format with optional ROI support
    /// </summary>
    Result<EncodedImageData> EncodeToJpeg2000(Image<Rgba32> image, Jpeg2000EncodingOptions options);
    
    /// <summary>
    /// Encodes an image to JPEG format
    /// </summary>
    Result<EncodedImageData> EncodeToJpeg(Image<Rgba32> image, int quality = 85);
    
    /// <summary>
    /// Encodes an image to PNG format
    /// </summary>
    Result<EncodedImageData> EncodeToPng(Image<Rgba32> image);
    
    /// <summary>
    /// Encodes an image to TIFF format
    /// </summary>
    Result<EncodedImageData> EncodeToTiff(Image<Rgba32> image);
}

/// <summary>
/// Result of image encoding
/// </summary>
[PublicAPI]
public sealed record EncodedImageData(
    ImmutableArray<byte> Data,
    string Format,
    int FileSize,
    ImageDimensions Dimensions);

/// <summary>
/// Options for JPEG 2000 encoding
/// </summary>
[PublicAPI]
public sealed record Jpeg2000EncodingOptions(
    EncodingStrategy Strategy,
    FacialRoiSet? RoiSet = null,
    int RoiStartLevel = 3,
    bool EnableRoi = true,
    bool AlignRoi = false);

/// <summary>
/// Implementation of pure encoding service
/// </summary>
[PublicAPI]
public sealed class BiometricEncodingService : IBiometricEncodingService
{
    private readonly IJpeg2000Encoder _jpeg2000Encoder;
    private readonly ILogger<BiometricEncodingService> _logger;

    public BiometricEncodingService(
        IJpeg2000Encoder jpeg2000Encoder,
        ILogger<BiometricEncodingService> logger)
    {
        _jpeg2000Encoder = jpeg2000Encoder;
        _logger = logger;
    }

    public Result<EncodedImageData> EncodeToJpeg2000(Image<Rgba32> image, Jpeg2000EncodingOptions options)
    {
        _logger.LogDebug("Encoding image to JPEG 2000: {Width}x{Height}", image.Width, image.Height);
        
        var roiSet = options.RoiSet ?? CreateDefaultRoiSet(image);
        
        var baseRate = options.Strategy switch
        {
            FixedRateStrategy fixedRate => fixedRate.Rate,
            TargetSizeStrategy targetSize => CalculateRateForTargetSize(
                targetSize.TargetBytes, image.Width, image.Height),
            _ => 1.0f
        };
        
        var encodeResult = _jpeg2000Encoder.EncodeWithRoi(
            image,
            roiSet,
            baseRate,
            options.RoiStartLevel,
            options.EnableRoi,
            options.AlignRoi
        );
        
        if (encodeResult.IsFailure)
            return Result.Failure<EncodedImageData>($"JPEG 2000 encoding failed: {encodeResult.Error}");
            
        var data = encodeResult.Value.ToImmutableArray();
        _logger.LogInformation("JPEG 2000 encoding complete: {Size} bytes", data.Length);
        
        return Result.Success(new EncodedImageData(
            data,
            "JP2",
            data.Length,
            new ImageDimensions(image.Width, image.Height)));
    }

    public Result<EncodedImageData> EncodeToJpeg(Image<Rgba32> image, int quality = 85)
    {
        if (quality < 1 || quality > 100)
            return Result.Failure<EncodedImageData>("JPEG quality must be between 1 and 100");
            
        return Result.Try(() =>
        {
            _logger.LogDebug("Encoding image to JPEG: {Width}x{Height}, quality: {Quality}", 
                image.Width, image.Height, quality);
            
            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder { Quality = quality });
            
            var data = ms.ToArray().ToImmutableArray();
            _logger.LogInformation("JPEG encoding complete: {Size} bytes", data.Length);
            
            return new EncodedImageData(
                data,
                "JPEG",
                data.Length,
                new ImageDimensions(image.Width, image.Height));
        });
    }

    public Result<EncodedImageData> EncodeToPng(Image<Rgba32> image)
    {
        return Result.Try(() =>
        {
            _logger.LogDebug("Encoding image to PNG: {Width}x{Height}", image.Width, image.Height);
            
            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());
            
            var data = ms.ToArray().ToImmutableArray();
            _logger.LogInformation("PNG encoding complete: {Size} bytes", data.Length);
            
            return new EncodedImageData(
                data,
                "PNG",
                data.Length,
                new ImageDimensions(image.Width, image.Height));
        });
    }

    public Result<EncodedImageData> EncodeToTiff(Image<Rgba32> image)
    {
        return Result.Try(() =>
        {
            _logger.LogDebug("Encoding image to TIFF: {Width}x{Height}", image.Width, image.Height);
            
            using var ms = new MemoryStream();
            image.Save(ms, new TiffEncoder());
            
            var data = ms.ToArray().ToImmutableArray();
            _logger.LogInformation("TIFF encoding complete: {Size} bytes", data.Length);
            
            return new EncodedImageData(
                data,
                "TIFF",
                data.Length,
                new ImageDimensions(image.Width, image.Height));
        });
    }

    private static FacialRoiSet CreateDefaultRoiSet(Image<Rgba32> image)
    {
        // Create a minimal ROI set for the entire image when no ROI is specified
        var roiBounds = new RoiBoundingBox(0, 0, image.Width, image.Height);
        var roiRegion = new RoiRegion("FullImage", 1, roiBounds, new List<int> { 0, 1, 2, 3, 4 });
        return new FacialRoiSet(roiRegion);
    }

    private static float CalculateRateForTargetSize(int targetBytes, int width, int height)
    {
        // Estimate compression rate needed for target size
        // This is a rough approximation: rate = (targetBytes * 8) / (width * height)
        var totalPixels = width * height;
        var targetBits = targetBytes * 8;
        return Math.Max(0.1f, Math.Min(5.0f, (float)targetBits / totalPixels));
    }
}