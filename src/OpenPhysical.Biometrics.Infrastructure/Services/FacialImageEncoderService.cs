using System.Drawing;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// Functional facial image encoder service using Result pattern for error handling.
/// This service provides backward compatibility while delegating to the new architecture.
/// For new code, use IBiometricProcessingService or IBiometricEncodingService directly.
/// </summary>
/// <remarks>This supported compatibility facade preserves existing consumers. New integrations can use IBiometricProcessingService or IBiometricEncodingService.</remarks>
[PublicAPI]
public sealed class FacialImageEncoderService : IDisposable
{
    private readonly IFaceDetector _faceDetector;
    private readonly ILandmarkExtractor? _landmarkExtractor;
    private readonly IJpeg2000Encoder _jpeg2000Encoder;
    private readonly IBiometricProcessingService? _processingService;
    private readonly ILogger<FacialImageEncoderService> _logger;

    /// <summary>Creates the service with its processing dependencies and operational diagnostics.</summary>
    /// <param name="faceDetector">Face detector used to select the subject region.</param>
    /// <param name="jpeg2000Encoder">JPEG 2000 encoder used for the selected compression profile.</param>
    /// <param name="logger">Operational diagnostics for this service.</param>
    public FacialImageEncoderService(
        IFaceDetector faceDetector,
        IJpeg2000Encoder jpeg2000Encoder,
        ILogger<FacialImageEncoderService> logger
    )
    {
        _faceDetector = faceDetector;
        _jpeg2000Encoder = jpeg2000Encoder;
        _logger = logger;
    }

    /// <summary>
    /// Constructor with landmark extractor and processing service
    /// </summary>
    public FacialImageEncoderService(
        IFaceDetector faceDetector,
        ILandmarkExtractor landmarkExtractor,
        IJpeg2000Encoder jpeg2000Encoder,
        IBiometricProcessingService processingService,
        ILogger<FacialImageEncoderService> logger
    )
    {
        _faceDetector = faceDetector;
        _landmarkExtractor = landmarkExtractor;
        _jpeg2000Encoder = jpeg2000Encoder;
        _processingService = processingService;
        _logger = logger;
    }

    /// <summary>
    /// Constructor with landmark extractor for backward compatibility
    /// </summary>
    public FacialImageEncoderService(
        IFaceDetector faceDetector,
        ILandmarkExtractor landmarkExtractor,
        IJpeg2000Encoder jpeg2000Encoder,
        ILogger<FacialImageEncoderService> logger
    )
        : this(faceDetector, jpeg2000Encoder, logger)
    {
        _landmarkExtractor = landmarkExtractor;
    }

    /// <summary>
    /// Process image bytes to PIV-compatible JPEG 2000 format using functional Result pattern
    /// </summary>
    /// <param name="imageData">Input image data (JPEG, PNG, etc.)</param>
    /// <param name="options">Processing options. Uses PIV standard if not specified.</param>
    /// <returns>Result containing processing result with encoded image data and metadata, or error message</returns>
    public async Task<Result<ProcessingResultDto>> ProcessAsync(
        byte[] imageData,
        ProcessingOptions? options = null
    )
    {
        return await ProcessAsync(imageData, options, "jp2", 85);
    }

    /// <summary>
    /// Process image bytes and encode in the specified format using functional Result pattern
    /// </summary>
    /// <param name="imageData">Input image data</param>
    /// <param name="options">Processing options. Uses PIV standard if not specified.</param>
    /// <param name="outputFormat">Output format: "jp2", "jpeg", "png", or "tiff"</param>
    /// <param name="jpegQuality">JPEG quality (1-100) when outputFormat is "jpeg"</param>
    /// <returns>Result containing processing result with encoded image data and metadata, or error message</returns>
    public async Task<Result<ProcessingResultDto>> ProcessAsync(
        byte[] imageData,
        ProcessingOptions? options,
        string outputFormat,
        int jpegQuality
    )
    {
        var processingOptions = options ?? ProcessingOptions.PivBalanced;

        var validationResult = ValidateInput(
            imageData,
            processingOptions,
            outputFormat,
            jpegQuality
        );
        if (validationResult.IsFailure)
            return Result.Failure<ProcessingResultDto>(validationResult.Error);

        // PIV compression applies to JPEG 2000. Other formats retain their own encoder settings.
        if (
            _processingService != null
            && outputFormat.Equals("jp2", StringComparison.OrdinalIgnoreCase)
        )
        {
            _logger.LogInformation(
                new EventId(357100, "FacialImageEncoderService"),
                "Using PIV processing pipeline."
            );
            return await _processingService.ProcessForPivAsync(
                imageData,
                processingOptions,
                outputFormat
            );
        }

        var processResult = await ProcessImageInternalAsync(
            imageData,
            processingOptions,
            outputFormat,
            jpegQuality
        );
        return processResult.Map(ConvertToDto);
    }

    /// <summary>
    /// Process image bytes for TWIC card compatibility (14KB maximum)
    /// </summary>
    /// <param name="imageData">Input image data</param>
    /// <returns>Result containing processing result optimized for TWIC requirements</returns>
    public async Task<Result<ProcessingResultDto>> ProcessForTwicAsync(byte[] imageData)
    {
        // TWIC uses PIV dimensions, so delegate to PIV processing if available
        if (_processingService != null)
        {
            return await _processingService.ProcessForPivAsync(
                imageData,
                ProcessingOptions.TwicMax
            );
        }
        return await ProcessAsync(imageData, ProcessingOptions.TwicMax);
    }

    /// <summary>
    /// Process image bytes for PIV card compatibility (20KB target)
    /// </summary>
    /// <param name="imageData">Input image data</param>
    /// <returns>Result containing processing result optimized for PIV requirements</returns>
    public async Task<Result<ProcessingResultDto>> ProcessForPivAsync(byte[] imageData)
    {
        // Always use PIV processing if available
        if (_processingService != null)
        {
            return await _processingService.ProcessForPivAsync(
                imageData,
                ProcessingOptions.PivBalanced
            );
        }
        return await ProcessAsync(imageData, ProcessingOptions.PivBalanced);
    }

    /// <summary>
    /// Process image bytes with custom target file size
    /// </summary>
    /// <param name="imageData">Input image data</param>
    /// <param name="targetSizeBytes">Target file size in bytes</param>
    /// <returns>Result containing processing result targeting specified file size</returns>
    public async Task<Result<ProcessingResultDto>> ProcessToSizeAsync(
        byte[] imageData,
        int targetSizeBytes
    )
    {
        if (targetSizeBytes <= 0)
            return Result.Failure<ProcessingResultDto>("Target size must be greater than zero");

        var options = ProcessingOptions.PivBalanced with
        {
            Strategy = EncodingStrategy.TargetSize(targetSizeBytes),
        };

        return await ProcessAsync(imageData, options);
    }

    /// <summary>
    /// Process image bytes with fixed compression rate
    /// </summary>
    /// <param name="imageData">Input image data</param>
    /// <param name="compressionRate">Compression rate in bits per pixel</param>
    /// <returns>Result containing processing result using specified compression rate</returns>
    public async Task<Result<ProcessingResultDto>> ProcessWithRateAsync(
        byte[] imageData,
        float compressionRate
    )
    {
        if (compressionRate <= 0)
            return Result.Failure<ProcessingResultDto>(
                "Compression rate must be greater than zero"
            );

        var options = ProcessingOptions.PivBalanced with
        {
            Strategy = EncodingStrategy.FixedRate(compressionRate),
        };

        return await ProcessAsync(imageData, options);
    }

    private Result ValidateInput(
        byte[] imageData,
        ProcessingOptions options,
        string outputFormat,
        int jpegQuality
    )
    {
        if (imageData is null || imageData.Length == 0)
            return Result.Failure("Image data cannot be empty");

        if (!float.IsFinite(options.MinFaceConfidence) || options.MinFaceConfidence is < 0 or > 1)
            return Result.Failure("MinFaceConfidence must be between 0 and 1");

        if (options.RoiStartLevel is < 0 or > 3)
            return Result.Failure("RoiStartLevel must be between 0 and 3");

        if (options.MaxRetries < 0)
            return Result.Failure("MaxRetries must be non-negative");

        var optionValidation = ValidateProcessingOptions(options);
        if (optionValidation.IsFailure)
            return optionValidation;

        if (!IsValidOutputFormat(outputFormat))
            return Result.Failure($"Unsupported output format: {outputFormat}");

        if (
            (
                outputFormat.Equals("jpeg", StringComparison.OrdinalIgnoreCase)
                || outputFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase)
            ) && jpegQuality is < 1 or > 100
        )
            return Result.Failure("JPEG quality must be between 1 and 100");

        return Result.Success();
    }

    private async Task<Result<ProcessingResult>> ProcessImageInternalAsync(
        byte[] imageData,
        ProcessingOptions options,
        string outputFormat,
        int jpegQuality
    )
    {
        return await Result
            .Try(
                async () =>
                {
                    var startTime = DateTime.UtcNow;

                    // Load and validate image
                    var loadImageResult = await LoadImageAsync(imageData);
                    if (loadImageResult.IsFailure)
                        return Result.Failure<ProcessingResult>(loadImageResult.Error);

                    using var image = loadImageResult.Value;

                    // Detect faces
                    var facesResult = await _faceDetector.DetectFacesAsync(image);
                    if (facesResult.IsFailure)
                        return Result.Failure<ProcessingResult>(
                            $"Face detection failed: {facesResult.Error}"
                        );

                    if (facesResult.Value.Count == 0)
                        return Result.Failure<ProcessingResult>("No faces detected in the image");

                    if (facesResult.Value.Count > 1 && options.RequireSingleFace)
                        return Result.Failure<ProcessingResult>(
                            $"Multiple faces detected ({facesResult.Value.Count}), but RequireSingleFace is true"
                        );

                    // Select the best face
                    var selectedFace = SelectBestFace(facesResult.Value, options.MinFaceConfidence);
                    if (selectedFace.IsFailure)
                        return Result.Failure<ProcessingResult>(selectedFace.Error);

                    // For simplicity, use the original image without complex transformations
                    // Real implementation would apply PIV transformations here

                    // Encode to target format
                    var encodeResult = await EncodeImageAsync(
                        image,
                        outputFormat,
                        jpegQuality,
                        options
                    );
                    if (encodeResult.IsFailure)
                        return Result.Failure<ProcessingResult>(
                            $"Image encoding failed: {encodeResult.Error}"
                        );

                    var processingTime = DateTime.UtcNow - startTime;
                    var metadata = CreateProcessingMetadata(
                        image,
                        selectedFace.Value,
                        encodeResult.Value.Length,
                        processingTime,
                        options
                    );

                    return Result.Success(new ProcessingResult(encodeResult.Value, metadata));
                },
                ex =>
                    ex switch
                    {
                        OperationCanceledException => "Processing exceeded timeout",
                        _ => $"Image processing failed: {ex.Message}",
                    }
            )
            .Bind(result => result);
    }

    private async Task<Result<RgbImage>> LoadImageAsync(byte[] imageData)
    {
        return await Result.Try(
            async () =>
            {
                await Task.CompletedTask;
                return RgbImage.Load(imageData);
            },
            ex => $"Invalid image data: {ex.Message}"
        );
    }

    private Result<DetectedFace> SelectBestFace(
        IReadOnlyList<DetectedFace> faces,
        float minConfidence
    )
    {
        var bestFace = faces
            .Where(f => f.Confidence >= minConfidence)
            .OrderByDescending(f => f.Confidence)
            .FirstOrDefault();

        if (bestFace == null)
            return Result.Failure<DetectedFace>(
                $"No faces meet the minimum confidence threshold of {minConfidence:P0}"
            );

        return Result.Success(bestFace);
    }

    private async Task<Result<byte[]>> EncodeImageAsync(
        RgbImage image,
        string outputFormat,
        int jpegQuality,
        ProcessingOptions options
    )
    {
        return outputFormat.ToLowerInvariant() switch
        {
            "jp2" => await EncodeAsJpeg2000Async(image, options),
            "jpeg" or "jpg" => await EncodeAsJpegAsync(image, jpegQuality),
            "png" => await EncodeAsPngAsync(image),
            "tiff" or "tif" => await EncodeAsTiffAsync(image),
            _ => Result.Failure<byte[]>($"Unsupported output format: {outputFormat}"),
        };
    }

    private async Task<Result<byte[]>> EncodeAsJpeg2000Async(
        RgbImage image,
        ProcessingOptions options
    )
    {
        return await Result.Try(
            async () =>
            {
                await Task.CompletedTask;

                // Create a minimal ROI set for the entire image
                var roiBounds = new RoiBoundingBox(0, 0, image.Width, image.Height);
                var roiRegion = new RoiRegion(
                    "FullImage",
                    1,
                    roiBounds,
                    new List<int> { 0, 1, 2, 3, 4 }
                );
                var roiSet = new FacialRoiSet(roiRegion);

                var baseRate = options.Strategy switch
                {
                    FixedRateStrategy fixedRate => fixedRate.Rate,
                    _ => 1.0f,
                };

                var result = new StrategyJpeg2000Encoder(_jpeg2000Encoder, options).EncodeWithRoi(
                    image,
                    roiSet,
                    baseRate,
                    options.RoiStartLevel,
                    options.EnableRoi,
                    options.AlignRoi
                );

                return result.IsSuccess
                    ? result.Value
                    : throw new InvalidOperationException(result.Error);
            },
            ex => $"JPEG 2000 encoding failed: {ex.Message}"
        );
    }

    /// <summary>
    /// Process image without face detection, preserving original dimensions
    /// </summary>
    /// <param name="imageData">Input image data</param>
    /// <param name="options">Processing options. Uses PIV standard if not specified.</param>
    /// <param name="outputFormat">Output format: "jp2", "jpeg", "png", or "tiff"</param>
    /// <param name="jpegQuality">JPEG quality (1-100) when outputFormat is "jpeg"</param>
    /// <returns>Processing result with encoded image data and metadata</returns>
    public async Task<Result<ProcessingResultDto>> ProcessWithoutFaceDetectionAsync(
        byte[] imageData,
        ProcessingOptions? options = null,
        string outputFormat = "jp2",
        int jpegQuality = 85
    )
    {
        var processingOptions = options ?? ProcessingOptions.PivBalanced;

        // Validate options
        var optionsValidation = ValidateProcessingOptions(processingOptions);
        if (optionsValidation.IsFailure)
            return Result.Failure<ProcessingResultDto>(optionsValidation.Error);

        // Validate JPEG quality
        if (
            (
                outputFormat.Equals("jpeg", StringComparison.OrdinalIgnoreCase)
                || outputFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase)
            ) && (jpegQuality < 1 || jpegQuality > 100)
        )
        {
            return Result.Failure<ProcessingResultDto>("JPEG quality must be between 1 and 100");
        }

        var startTime = DateTime.UtcNow;

        return await LoadImage(imageData)
            .Bind(async image =>
            {
                using var ownedImage = image;
                _logger.LogDebug(
                    new EventId(357101, "FacialImageEncoderService"),
                    "FacialImageEncoderService processing status changed."
                );

                Result<byte[]> encodingResult;
                float compressionRate = 0;

                if (
                    outputFormat.Equals("jpeg", StringComparison.OrdinalIgnoreCase)
                    || outputFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase)
                )
                {
                    _logger.LogDebug(
                        new EventId(357102, "FacialImageEncoderService"),
                        "FacialImageEncoderService processing status changed."
                    );
                    encodingResult = await EncodeAsJpegAsync(image, jpegQuality);
                }
                else if (outputFormat.Equals("png", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        new EventId(357103, "FacialImageEncoderService"),
                        "Encoding as PNG"
                    );
                    encodingResult = await EncodeAsPngAsync(image);
                }
                else if (
                    outputFormat.Equals("tiff", StringComparison.OrdinalIgnoreCase)
                    || outputFormat.Equals("tif", StringComparison.OrdinalIgnoreCase)
                )
                {
                    _logger.LogDebug(
                        new EventId(357104, "FacialImageEncoderService"),
                        "Encoding as TIFF"
                    );
                    encodingResult = await EncodeAsTiffAsync(image);
                }
                else if (outputFormat.Equals("jp2", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        new EventId(357105, "FacialImageEncoderService"),
                        "Encoding as JPEG 2000 with dynamic ROI calculation"
                    );

                    // Calculate ROI based on image dimensions using Appendix C.6 formula
                    var roiSet = FacialRoiSet.CalculateRoiForDimensions(image.Width, image.Height);

                    var jp2Result = await ExecuteEncodingStrategy(
                        image,
                        roiSet,
                        processingOptions,
                        CancellationToken.None
                    );

                    if (jp2Result.IsFailure)
                        return Result.Failure<ProcessingResultDto>(jp2Result.Error);

                    compressionRate = jp2Result.Value.ActualRate;
                    encodingResult = Result.Success(jp2Result.Value.Data);
                }
                else
                {
                    return Result.Failure<ProcessingResultDto>(
                        $"Unsupported output format: {outputFormat}"
                    );
                }

                return encodingResult.Map(encodedData =>
                {
                    var processingTime = DateTime.UtcNow - startTime;
                    _logger.LogInformation(
                        new EventId(357106, "FacialImageEncoderService"),
                        "FacialImageEncoderService processing completed."
                    );

                    // Build metadata
                    var metadata = new ProcessingMetadataDto(
                        new ImageDimensions(image.Width, image.Height),
                        0f, // No rotation applied
                        1f, // No face detection, so confidence is N/A (use 1.0)
                        encodedData.Length,
                        processingTime
                    )
                    {
                        CompressionRate = compressionRate,
                        AdditionalData = new Dictionary<string, object>
                        {
                            ["ProcessingMode"] = "NoResize",
                            ["OutputFormat"] = outputFormat.ToUpperInvariant(),
                        },
                    };

                    return new ProcessingResultDto(encodedData, metadata);
                });
            });
    }

    private async Task<Result<byte[]>> EncodeAsJpegAsync(RgbImage image, int quality)
    {
        return await Result.Try(
            async () =>
            {
                await Task.CompletedTask;
                return ImageToBytes(image, "jpeg", quality);
            },
            ex => $"JPEG encoding failed: {ex.Message}"
        );
    }

    private async Task<Result<byte[]>> EncodeAsPngAsync(RgbImage image)
    {
        return await Result.Try(
            async () =>
            {
                await Task.CompletedTask;
                using var ms = new MemoryStream();
                await image.SaveAsPngAsync(ms);
                return ms.ToArray();
            },
            ex => $"PNG encoding failed: {ex.Message}"
        );
    }

    private async Task<Result<byte[]>> EncodeAsTiffAsync(RgbImage image)
    {
        return await Result.Try(
            async () =>
            {
                await Task.CompletedTask;
                using var ms = new MemoryStream();
                await image.SaveAsTiffAsync(ms);
                return ms.ToArray();
            },
            ex => $"TIFF encoding failed: {ex.Message}"
        );
    }

    private static byte[] ImageToBytes(RgbImage image, string format, int quality = 95)
    {
        using var ms = new MemoryStream();

        switch (format.ToLowerInvariant())
        {
            case "jpeg":
                image.SaveAsJpeg(ms, quality);
                break;
            case "png":
                image.SaveAsPng(ms);
                break;
            case "tiff":
                image.SaveAsTiff(ms);
                break;
            default:
                throw new ArgumentException($"Unsupported format: {format}");
        }

        return ms.ToArray();
    }

    private static ProcessingMetadata CreateProcessingMetadata(
        RgbImage image,
        DetectedFace face,
        int fileSize,
        TimeSpan processingTime,
        ProcessingOptions options
    )
    {
        return new ProcessingMetadata(
            OutputDimensions: new ImageDimensions(image.Width, image.Height),
            RotationApplied: 0, // Simplified - no rotation tracking
            FaceConfidence: face.Confidence,
            FileSize: fileSize,
            ProcessingTime: processingTime
        )
        {
            CompressionRate = CalculateCompressionRate(image, fileSize),
            TargetSize = options.Strategy is TargetSizeStrategy ts
                ? Maybe<int>.From(ts.TargetBytes)
                : Maybe<int>.None,
            Warnings = new List<string>(),
            AdditionalData = new Dictionary<string, object>
            {
                ["FaceBounds"] = face.BoundingBox,
                ["ProcessingOptions"] = options,
            },
        };
    }

    private static float CalculateCompressionRate(RgbImage image, int fileSize)
    {
        var uncompressedSize = image.Width * image.Height * 3; // RGB, 8 bits per channel
        return fileSize > 0 ? (float)uncompressedSize / fileSize : 0;
    }

    private static bool IsValidOutputFormat(string format)
    {
        return format?.ToLowerInvariant() switch
        {
            "jp2" or "jpeg" or "jpg" or "png" or "tiff" or "tif" => true,
            _ => false,
        };
    }

    private Result<RgbImage> LoadImage(byte[] imageData)
    {
        return Result.Try(
            () => RgbImage.Load(imageData),
            ex => $"Invalid image data: {ex.Message}"
        );
    }

    private Result ValidateProcessingOptions(ProcessingOptions options)
    {
        if (!float.IsFinite(options.MinFaceConfidence) || options.MinFaceConfidence is < 0 or > 1)
            return Result.Failure("MinFaceConfidence must be between 0 and 1");

        if (!float.IsFinite(options.MaxRotationDegrees) || options.MaxRotationDegrees < 0)
            return Result.Failure("MaxRotationDegrees must be non-negative");

        if (options.RoiStartLevel is < 0 or > 3)
            return Result.Failure("RoiStartLevel must be between 0 and 3");

        if (options.MaxRetries < 0)
            return Result.Failure("MaxRetries must be non-negative");

        if (
            options.Strategy is FixedRateStrategy fixedRate
            && (!float.IsFinite(fixedRate.Rate) || fixedRate.Rate <= 0)
        )
            return Result.Failure("Compression rate must be finite and greater than zero");

        if (options.Strategy is TargetSizeStrategy target && target.TargetBytes <= 0)
            return Result.Failure("Target size must be greater than zero");

        if (options.ProcessingTimeout <= TimeSpan.Zero)
            return Result.Failure("ProcessingTimeout must be positive");

        return Result.Success();
    }

    private async Task<Result<(byte[] Data, float ActualRate)>> ExecuteEncodingStrategy(
        RgbImage image,
        FacialRoiSet roiSet,
        ProcessingOptions options,
        CancellationToken cancellationToken
    )
    {
        return await Result.Try(
            async () =>
            {
                await Task.CompletedTask;

                var baseRate = options.Strategy switch
                {
                    FixedRateStrategy fixedRate => fixedRate.Rate,
                    TargetSizeStrategy targetSize => EstimateRateForTargetSize(
                        image,
                        targetSize.TargetBytes
                    ),
                    _ => 1.0f,
                };

                var result = new StrategyJpeg2000Encoder(_jpeg2000Encoder, options).EncodeWithRoi(
                    image,
                    roiSet,
                    baseRate,
                    options.RoiStartLevel,
                    options.EnableRoi,
                    options.AlignRoi
                );

                if (result.IsFailure)
                    throw new InvalidOperationException(result.Error);

                return (result.Value, baseRate);
            },
            ex => $"JPEG 2000 encoding failed: {ex.Message}"
        );
    }

    private static float EstimateRateForTargetSize(RgbImage image, int targetBytes)
    {
        // Estimate compression rate based on target size
        // This is a simplified estimation - real implementation would be more sophisticated
        var uncompressedSize = image.Width * image.Height * 3; // RGB
        var compressionRatio = (float)targetBytes / uncompressedSize;

        // Convert to bits per pixel
        return compressionRatio * 24f; // 24 bits per pixel (RGB)
    }

    private static ProcessingResultDto ConvertToDto(ProcessingResult result)
    {
        var metadataDto = new ProcessingMetadataDto(
            result.Metadata.OutputDimensions,
            result.Metadata.RotationApplied,
            result.Metadata.FaceConfidence,
            result.Metadata.FileSize,
            result.Metadata.ProcessingTime
        )
        {
            CompressionRate = result.Metadata.CompressionRate,
            TargetSize = result.Metadata.TargetSize.HasValue
                ? result.Metadata.TargetSize.Value
                : null,
            Warnings = result.Metadata.Warnings,
            AdditionalData = result.Metadata.AdditionalData,
        };

        return new ProcessingResultDto(result.ImageData, metadataDto);
    }

    /// <summary>Releases resources owned by the compatibility processing facade.</summary>
    public void Dispose()
    {
        if (_faceDetector is IDisposable disposableDetector)
            disposableDetector.Dispose();
        // JPEG2000 encoder is typically stateless, no disposal needed
    }
}
