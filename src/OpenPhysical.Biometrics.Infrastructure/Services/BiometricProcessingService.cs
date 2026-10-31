using System.Collections.Immutable;
using System.Drawing;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Standards;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Imaging;

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
        string outputFormat = "jp2"
    );

    /// <summary>
    /// Process image with custom dimensions and transformations
    /// </summary>
    Task<Result<ProcessingResultDto>> ProcessForStandardAsync(
        byte[] imageData,
        ImageDimensions targetDimensions,
        ProcessingOptions? options = null,
        string outputFormat = "jp2"
    );

    /// <summary>
    /// Process image with encoding only (no transformations)
    /// </summary>
    Task<Result<ProcessingResultDto>> ProcessWithEncodingOnlyAsync(
        byte[] imageData,
        ProcessingOptions? options = null,
        string outputFormat = "jp2",
        int jpegQuality = 85
    );
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

    /// <summary>Creates the service with its processing dependencies and operational diagnostics.</summary>
    /// <param name="faceDetector">Face detector used to select the subject region.</param>
    /// <param name="landmarkExtractor">Facial landmark extractor used to align the subject.</param>
    /// <param name="jpeg2000Encoder">JPEG 2000 encoder used for the selected compression profile.</param>
    /// <param name="encodingService">Image encoder for the requested output format.</param>
    /// <param name="logger">Operational diagnostics for this service.</param>
    public BiometricProcessingService(
        IFaceDetector faceDetector,
        ILandmarkExtractor landmarkExtractor,
        IJpeg2000Encoder jpeg2000Encoder,
        IBiometricEncodingService encodingService,
        ILogger<BiometricProcessingService> logger
    )
    {
        _faceDetector = faceDetector;
        _landmarkExtractor = landmarkExtractor;
        _jpeg2000Encoder = jpeg2000Encoder;
        _encodingService = encodingService;
        _logger = logger;
    }

    /// <summary>Detects, aligns and encodes a facial capture using the PIV processing profile.</summary>
    /// <param name="imageData">Encoded source image bytes.</param>
    /// <param name="options">Processing and compression settings, or null to use the profile defaults.</param>
    /// <param name="outputFormat">Requested output format.</param>
    /// <returns>The encoded image and its output metadata, or a processing failure.</returns>
    public async Task<Result<ProcessingResultDto>> ProcessForPivAsync(
        byte[] imageData,
        ProcessingOptions? options = null,
        string outputFormat = "jp2"
    )
    {
        var processingOptions = options ?? ProcessingOptions.PivBalanced;
        var started = System.Diagnostics.Stopwatch.StartNew();
        if (imageData is null || imageData.Length == 0)
            return Result.Failure<ProcessingResultDto>("Invalid image data: the input is empty.");
        if (
            outputFormat.ToLowerInvariant()
            is not ("jp2" or "jpeg" or "jpg" or "png" or "tiff" or "tif")
        )
            return Result.Failure<ProcessingResultDto>(
                "The requested portrait output format is unsupported."
            );

        try
        {
            _logger.LogInformation(
                new EventId(864400, "BiometricProcessingService"),
                "Starting PIV processing pipeline"
            );

            // Load image
            RgbImage sourceImage;
            try
            {
                sourceImage = RgbImage.Load(imageData);
            }
            catch (ArgumentException)
            {
                return Result.Failure<ProcessingResultDto>(
                    "Invalid image data: the encoded image is malformed or unsupported."
                );
            }
            using var ownedSource = sourceImage;

            // Use PivProcessor for standard PIV transformation
            var pivResult = await PivProcessor.ProcessAsync(
                sourceImage,
                _faceDetector,
                _landmarkExtractor,
                new StrategyJpeg2000Encoder(_jpeg2000Encoder, processingOptions),
                PivProcessingOptions.Default with
                {
                    MinFaceConfidence = processingOptions.MinFaceConfidence,
                    RequireSingleFace = processingOptions.RequireSingleFace,
                    MaxRotationDegrees = processingOptions.MaxRotationDegrees,
                    RoiStartLevel = processingOptions.RoiStartLevel,
                },
                processingOptions.EnableRoi,
                processingOptions.AlignRoi,
                _logger
            );

            if (pivResult.IsFailure)
                return Result.Failure<ProcessingResultDto>(
                    $"PIV processing failed: {pivResult.Error}"
                );

            var pivData = pivResult.Value;
            using var processedImage = pivData.Metadata.TryGetValue("PivImage", out var pixels)
                ? pixels as RgbImage
                : null;
            if (started.Elapsed > processingOptions.ProcessingTimeout)
                return Result.Failure<ProcessingResultDto>("Processing exceeded timeout");
            var outputData = pivData.ImageData;
            if (!outputFormat.Equals("jp2", StringComparison.OrdinalIgnoreCase))
            {
                if (processedImage is null)
                    return Result.Failure<ProcessingResultDto>(
                        "The aligned portrait pixels are unavailable."
                    );
                var encoded = outputFormat.ToLowerInvariant() switch
                {
                    "jpeg" or "jpg" => _encodingService.EncodeToJpeg(processedImage),
                    "png" => _encodingService.EncodeToPng(processedImage),
                    _ => _encodingService.EncodeToTiff(processedImage),
                };
                if (encoded.IsFailure)
                    return Result.Failure<ProcessingResultDto>(encoded.Error);
                outputData = encoded.Value.Data.ToArray();
            }

            if (started.Elapsed > processingOptions.ProcessingTimeout)
                return Result.Failure<ProcessingResultDto>("Processing exceeded timeout");

            // Report the actual selected encoding and elapsed processing work.
            var metadata = new ProcessingMetadataDto(
                OutputDimensions: new ImageDimensions(PivConstants.Width, PivConstants.Height),
                RotationApplied: pivData.AppliedTransform.RotationDegrees,
                FaceConfidence: pivData.SourceFace.Confidence,
                FileSize: outputData.Length,
                ProcessingTime: started.Elapsed
            )
            {
                CompressionRate = CalculateCompressionRatio(
                    outputData.Length,
                    PivConstants.Width * PivConstants.Height * 3
                ),
                Warnings = pivData.Warnings.ToList(),
                AdditionalData = new Dictionary<string, object>
                {
                    ["Standard"] = "PIV",
                    ["SourceDimensions"] = new ImageDimensions(
                        sourceImage.Width,
                        sourceImage.Height
                    ),
                    ["AppliedTransform"] = pivData.AppliedTransform,
                    ["IsPivCompliant"] = pivData.IsPivCompliant,
                },
            };

            return Result.Success(new ProcessingResultDto(outputData, metadata));
        }
        catch (Exception ex)
        {
            return Result.Failure<ProcessingResultDto>($"PIV processing failed: {ex.Message}");
        }
    }

    /// <summary>Processes a facial capture for the supplied standard image dimensions.</summary>
    /// <param name="imageData">Encoded source image bytes.</param>
    /// <param name="targetDimensions">Required output dimensions in pixels.</param>
    /// <param name="options">Processing and compression settings, or null to use the profile defaults.</param>
    /// <param name="outputFormat">Requested output format.</param>
    /// <returns>The encoded image and its output metadata, or a processing failure.</returns>
    public async Task<Result<ProcessingResultDto>> ProcessForStandardAsync(
        byte[] imageData,
        ImageDimensions targetDimensions,
        ProcessingOptions? options = null,
        string outputFormat = "jp2"
    )
    {
        var processingOptions = options ?? ProcessingOptions.PivBalanced;

        try
        {
            _logger.LogInformation(
                new EventId(864401, "BiometricProcessingService"),
                "BiometricProcessingService processing status changed."
            );

            using var sourceImage = RgbImage.Load(imageData);

            // Detect faces
            var facesResult = await _faceDetector.DetectFacesAsync(sourceImage);
            if (facesResult.IsFailure)
                return Result.Failure<ProcessingResultDto>(
                    $"Face detection failed: {facesResult.Error}"
                );

            var faces = facesResult.Value;
            if (faces.Count == 0)
                return Result.Failure<ProcessingResultDto>("No faces detected");

            var bestFace = faces.OrderByDescending(f => f.Confidence).First();

            // Simple resize to target dimensions (no complex transformations)
            using var resizedImage = sourceImage.Clone();
            resizedImage.Mutate(x => x.Resize(targetDimensions.Width, targetDimensions.Height));

            // Encode
            var encodeResult = await EncodeImageAsync(
                resizedImage,
                outputFormat,
                processingOptions,
                null
            );
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
                CompressionRate = CalculateCompressionRatio(
                    encoded.FileSize,
                    targetDimensions.Width * targetDimensions.Height * 3
                ),
                AdditionalData = new Dictionary<string, object>
                {
                    ["SourceDimensions"] = new ImageDimensions(
                        sourceImage.Width,
                        sourceImage.Height
                    ),
                },
            };

            return Result.Success(new ProcessingResultDto(encoded.Data.ToArray(), metadata));
        }
        catch (Exception ex)
        {
            return Result.Failure<ProcessingResultDto>($"Standard processing failed: {ex.Message}");
        }
    }

    /// <summary>Encodes a facial capture while preserving its source dimensions.</summary>
    /// <param name="imageData">Encoded source image bytes.</param>
    /// <param name="options">Processing and compression settings, or null to use the profile defaults.</param>
    /// <param name="outputFormat">Requested output format.</param>
    /// <param name="jpegQuality">JPEG encoder quality from one through 100.</param>
    /// <returns>The encoded image and its output metadata, or a processing failure.</returns>
    public async Task<Result<ProcessingResultDto>> ProcessWithEncodingOnlyAsync(
        byte[] imageData,
        ProcessingOptions? options = null,
        string outputFormat = "jp2",
        int jpegQuality = 85
    )
    {
        var processingOptions = options ?? ProcessingOptions.PivBalanced;

        try
        {
            _logger.LogInformation(
                new EventId(864402, "BiometricProcessingService"),
                "Processing image with encoding only (no transformations)"
            );

            using var image = RgbImage.Load(imageData);

            // Encode without any transformations
            var encodeResult = outputFormat.ToLowerInvariant() switch
            {
                "jp2" => _encodingService.EncodeToJpeg2000(
                    image,
                    CreateJpeg2000Options(processingOptions)
                ),
                "jpeg" or "jpg" => _encodingService.EncodeToJpeg(image, jpegQuality),
                "png" => _encodingService.EncodeToPng(image),
                "tiff" or "tif" => _encodingService.EncodeToTiff(image),
                _ => Result.Failure<EncodedImageData>($"Unsupported format: {outputFormat}"),
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
                CompressionRate = CalculateCompressionRatio(
                    encoded.FileSize,
                    image.Width * image.Height * 3
                ),
            };

            return Result.Success(new ProcessingResultDto(encoded.Data.ToArray(), metadata));
        }
        catch (Exception ex)
        {
            return Result.Failure<ProcessingResultDto>(
                $"Encoding-only processing failed: {ex.Message}"
            );
        }
    }

    private async Task<Result<EncodedImageData>> EncodeImageAsync(
        RgbImage image,
        string outputFormat,
        ProcessingOptions options,
        FacialRoiSet? roiSet
    )
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
                    options.AlignRoi
                )
            ),
            "jpeg" or "jpg" => _encodingService.EncodeToJpeg(image, 85),
            "png" => _encodingService.EncodeToPng(image),
            "tiff" or "tif" => _encodingService.EncodeToTiff(image),
            _ => Result.Failure<EncodedImageData>($"Unsupported format: {outputFormat}"),
        };
    }

    private static Jpeg2000EncodingOptions CreateJpeg2000Options(ProcessingOptions options)
    {
        return new Jpeg2000EncodingOptions(
            options.Strategy,
            null,
            options.RoiStartLevel,
            options.EnableRoi,
            options.AlignRoi
        );
    }

    private static float CalculateCompressionRatio(int compressedSize, int uncompressedSize)
    {
        return uncompressedSize > 0 ? (float)uncompressedSize / compressedSize : 0;
    }
}
