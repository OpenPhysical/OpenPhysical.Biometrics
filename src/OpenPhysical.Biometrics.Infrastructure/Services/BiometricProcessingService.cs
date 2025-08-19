using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Core.Domain.Standards;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// Service that orchestrates biometric image processing with standard-specific transformations.
/// Separates concerns: detection, transformation, and encoding.
/// </summary>
[PublicAPI]
public interface IBiometricProcessingService
{
    /// <summary>
    /// Process image for PIV standard (420x560 with face detection and alignment)
    /// </summary>
    Task<Result<ProcessingResultDto>> ProcessForPivAsync(
        byte[] imageData,
        ProcessingOptions? options = null,
        string outputFormat = "jp2");
        
    /// <summary>
    /// Process image with custom dimensions and transformations
    /// </summary>
    Task<Result<ProcessingResultDto>> ProcessForStandardAsync(
        byte[] imageData,
        ImageDimensions targetDimensions,
        ProcessingOptions? options = null,
        string outputFormat = "jp2");
        
    /// <summary>
    /// Process image with encoding only (no transformations)
    /// </summary>
    Task<Result<ProcessingResultDto>> ProcessWithEncodingOnlyAsync(
        byte[] imageData,
        ProcessingOptions? options = null,
        string outputFormat = "jp2",
        int jpegQuality = 85);
}

/// <summary>
/// Implementation that orchestrates biometric processing pipeline
/// </summary>
[PublicAPI]
public sealed class BiometricProcessingService : IBiometricProcessingService
{
    private readonly IFaceDetector _faceDetector;
    private readonly ILandmarkExtractor _landmarkExtractor;
    private readonly IJpeg2000Encoder _jpeg2000Encoder;
    private readonly IBiometricEncodingService _encodingService;
    private readonly ILogger<BiometricProcessingService> _logger;

    public BiometricProcessingService(
        IFaceDetector faceDetector,
        ILandmarkExtractor landmarkExtractor,
        IJpeg2000Encoder jpeg2000Encoder,
        IBiometricEncodingService encodingService,
        ILogger<BiometricProcessingService> logger)
    {
        _faceDetector = faceDetector;
        _landmarkExtractor = landmarkExtractor;
        _jpeg2000Encoder = jpeg2000Encoder;
        _encodingService = encodingService;
        _logger = logger;
    }

    public async Task<Result<ProcessingResultDto>> ProcessForPivAsync(
        byte[] imageData,
        ProcessingOptions? options = null,
        string outputFormat = "jp2")
    {
        var processingOptions = options ?? ProcessingOptions.PivBalanced;
        
        try
        {
            _logger.LogInformation("Starting PIV processing pipeline");
            
            // Load image
            using var sourceImage = Image.Load<Rgba32>(imageData);
            
            // Use PivProcessor for standard PIV transformation
            var pivResult = await PivProcessor.ProcessAsync(
                sourceImage,
                _faceDetector,
                _landmarkExtractor,
                _jpeg2000Encoder,
                PivProcessingOptions.Default,
                processingOptions.EnableRoi,
                processingOptions.AlignRoi,
                _logger);
                
            if (pivResult.IsFailure)
                return Result.Failure<ProcessingResultDto>($"PIV processing failed: {pivResult.Error}");
            
            var pivData = pivResult.Value;
            
            // PIV processing already includes encoding, so we can use the result directly
            // The PivResult.ImageData is already encoded according to the format
            
            // Create result DTO
            var metadata = new ProcessingMetadataDto(
                OutputDimensions: new ImageDimensions(PivConstants.Width, PivConstants.Height),
                RotationApplied: pivData.AppliedTransform.RotationDegrees,
                FaceConfidence: pivData.SourceFace.Confidence,
                FileSize: pivData.ImageData.Length,
                ProcessingTime: TimeSpan.Zero
            )
            {
                CompressionRate = CalculateCompressionRatio(pivData.ImageData.Length, PivConstants.Width * PivConstants.Height * 3),
                Warnings = pivData.Warnings.ToList(),
                AdditionalData = new Dictionary<string, object>
                {
                    ["Standard"] = "PIV",
                    ["SourceDimensions"] = new ImageDimensions(sourceImage.Width, sourceImage.Height),
                    ["AppliedTransform"] = pivData.AppliedTransform,
                    ["IsPivCompliant"] = pivData.IsPivCompliant
                }
            };
            
            return Result.Success(new ProcessingResultDto(pivData.ImageData, metadata));
        }
        catch (Exception ex)
        {
            return Result.Failure<ProcessingResultDto>($"PIV processing failed: {ex.Message}");
        }
    }

    public async Task<Result<ProcessingResultDto>> ProcessForStandardAsync(
        byte[] imageData,
        ImageDimensions targetDimensions,
        ProcessingOptions? options = null,
        string outputFormat = "jp2")
    {
        var processingOptions = options ?? ProcessingOptions.PivBalanced;
        
        try
        {
            _logger.LogInformation("Processing image to {Width}x{Height}", 
                targetDimensions.Width, targetDimensions.Height);
            
            using var sourceImage = Image.Load<Rgba32>(imageData);
            
            // Detect faces
            var facesResult = await _faceDetector.DetectFacesAsync(sourceImage);
            if (facesResult.IsFailure)
                return Result.Failure<ProcessingResultDto>($"Face detection failed: {facesResult.Error}");
                
            var faces = facesResult.Value;
            if (faces.Count == 0)
                return Result.Failure<ProcessingResultDto>("No faces detected");
                
            var bestFace = faces.OrderByDescending(f => f.Confidence).First();
            
            // Simple resize to target dimensions (no complex transformations)
            using var resizedImage = sourceImage.Clone();
            resizedImage.Mutate(x => x.Resize(targetDimensions.Width, targetDimensions.Height));
            
            // Encode
            var encodeResult = await EncodeImageAsync(resizedImage, outputFormat, processingOptions, null);
            if (encodeResult.IsFailure)
                return Result.Failure<ProcessingResultDto>(encodeResult.Error);
                
            var encoded = encodeResult.Value;
            
            var metadata = new ProcessingMetadataDto(
                OutputDimensions: targetDimensions,
                RotationApplied: 0, // No rotation applied in simple resize
                FaceConfidence: bestFace.Confidence,
                FileSize: encoded.FileSize,
                ProcessingTime: TimeSpan.Zero
            )
            {
                CompressionRate = CalculateCompressionRatio(encoded.FileSize, targetDimensions.Width * targetDimensions.Height * 3),
                AdditionalData = new Dictionary<string, object>
                {
                    ["SourceDimensions"] = new ImageDimensions(sourceImage.Width, sourceImage.Height)
                }
            };
            
            return Result.Success(new ProcessingResultDto(encoded.Data.ToArray(), metadata));
        }
        catch (Exception ex)
        {
            return Result.Failure<ProcessingResultDto>($"Standard processing failed: {ex.Message}");
        }
    }

    public async Task<Result<ProcessingResultDto>> ProcessWithEncodingOnlyAsync(
        byte[] imageData,
        ProcessingOptions? options = null,
        string outputFormat = "jp2",
        int jpegQuality = 85)
    {
        var processingOptions = options ?? ProcessingOptions.PivBalanced;
        
        try
        {
            _logger.LogInformation("Processing image with encoding only (no transformations)");
            
            using var image = Image.Load<Rgba32>(imageData);
            
            // Encode without any transformations
            var encodeResult = outputFormat.ToLowerInvariant() switch
            {
                "jp2" => _encodingService.EncodeToJpeg2000(image, CreateJpeg2000Options(processingOptions)),
                "jpeg" or "jpg" => _encodingService.EncodeToJpeg(image, jpegQuality),
                "png" => _encodingService.EncodeToPng(image),
                "tiff" or "tif" => _encodingService.EncodeToTiff(image),
                _ => Result.Failure<EncodedImageData>($"Unsupported format: {outputFormat}")
            };
            
            if (encodeResult.IsFailure)
                return Result.Failure<ProcessingResultDto>(encodeResult.Error);
                
            var encoded = encodeResult.Value;
            
            var metadata = new ProcessingMetadataDto(
                OutputDimensions: new ImageDimensions(image.Width, image.Height),
                RotationApplied: 0, // No transformations applied
                FaceConfidence: 0, // No face detection performed
                FileSize: encoded.FileSize,
                ProcessingTime: TimeSpan.Zero
            )
            {
                CompressionRate = CalculateCompressionRatio(encoded.FileSize, image.Width * image.Height * 3)
            };
            
            return Result.Success(new ProcessingResultDto(encoded.Data.ToArray(), metadata));
        }
        catch (Exception ex)
        {
            return Result.Failure<ProcessingResultDto>($"Encoding-only processing failed: {ex.Message}");
        }
    }

    private async Task<Result<EncodedImageData>> EncodeImageAsync(
        Image<Rgba32> image,
        string outputFormat,
        ProcessingOptions options,
        FacialRoiSet? roiSet)
    {
        return outputFormat.ToLowerInvariant() switch
        {
            "jp2" => _encodingService.EncodeToJpeg2000(
                image, 
                new Jpeg2000EncodingOptions(
                    options.Strategy,
                    roiSet,
                    options.RoiStartLevel,
                    options.EnableRoi,
                    options.AlignRoi)),
            "jpeg" or "jpg" => _encodingService.EncodeToJpeg(image, 85),
            "png" => _encodingService.EncodeToPng(image),
            "tiff" or "tif" => _encodingService.EncodeToTiff(image),
            _ => Result.Failure<EncodedImageData>($"Unsupported format: {outputFormat}")
        };
    }

    private static Jpeg2000EncodingOptions CreateJpeg2000Options(ProcessingOptions options)
    {
        return new Jpeg2000EncodingOptions(
            options.Strategy,
            null,
            options.RoiStartLevel,
            options.EnableRoi,
            options.AlignRoi);
    }

    private static float CalculateCompressionRatio(int compressedSize, int uncompressedSize)
    {
        return uncompressedSize > 0 ? (float)uncompressedSize / compressedSize : 0;
    }
}