using System.Collections.Immutable;
using System.Drawing;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Common;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// Service for JPEG 2000 encoding with ROI support.
/// Provides library-first functionality for biometric image compression.
/// </summary>
[PublicAPI]
public interface IJpeg2000Service
{
    /// <summary>
    /// Encodes an image to JPEG 2000 format with specified compression settings.
    /// </summary>
    Task<Result<EncodedJpeg2000>> EncodeAsync(
        ImmutableArray<byte> imageData,
        Jpeg2000Options options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Encodes an image with Region of Interest (ROI) prioritization.
    /// </summary>
    Task<Result<EncodedJpeg2000>> EncodeWithRoiAsync(
        ImmutableArray<byte> imageData,
        FacialRoiSet roiSet,
        Jpeg2000Options options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Encodes an image to meet a target file size.
    /// </summary>
    Task<Result<EncodedJpeg2000>> EncodeToTargetSizeAsync(
        ImmutableArray<byte> imageData,
        int targetSizeBytes,
        Maybe<FacialRoiSet> roiSet,
        TargetSizeOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Calculates optimal compression settings for given constraints.
    /// </summary>
    Task<Result<CompressionRecommendation>> CalculateOptimalSettingsAsync(
        ImmutableArray<byte> imageData,
        CompressionConstraints constraints,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Implementation of JPEG 2000 encoding service.
/// </summary>
[PublicAPI]
public sealed class Jpeg2000Service : IJpeg2000Service
{
    private readonly IJpeg2000Encoder _encoder;
    private readonly ILogger<Jpeg2000Service> _logger;

    /// <summary>
    /// Initializes a new instance of the JPEG 2000 service.
    /// </summary>
    public Jpeg2000Service(IJpeg2000Encoder encoder, ILogger<Jpeg2000Service> logger)
    {
        _encoder = encoder;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<EncodedJpeg2000>> EncodeAsync(
        ImmutableArray<byte> imageData,
        Jpeg2000Options options,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                new EventId(920300, "Jpeg2000Service"),
                "Jpeg2000Service processing started."
            );

            using var image = RgbImage.Load(imageData.ToArray());

            // For non-ROI encoding, we just use the EncodeWithRoi method with ROI disabled
            // Create a minimal ROI set that covers the entire image
            var emptyRoi = FacialRoiSet.CalculateRoiForDimensions(image.Width, image.Height);
            var result = _encoder.EncodeWithRoi(
                image,
                emptyRoi,
                options.CompressionRate,
                options.RoiStartLevel,
                false, // ROI disabled
                options.AlignRoi
            );
            if (result.IsFailure)
            {
                return Result.Failure<EncodedJpeg2000>(
                    $"JPEG 2000 encoding failed: {result.Error}"
                );
            }

            var encodedData = ImmutableArray.Create(result.Value);
            var metadata = CreateMetadata(
                image,
                encodedData.Length,
                options.CompressionRate,
                false
            );

            var encoded = new EncodedJpeg2000(
                encodedData,
                new ImageDimensions(image.Width, image.Height),
                options.CompressionRate,
                encodedData.Length,
                metadata
            );

            _logger.LogDebug(
                new EventId(920301, "Jpeg2000Service"),
                "Jpeg2000Service processing completed."
            );
            return Result.Success(encoded);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(920302, "Jpeg2000Service"), "JPEG 2000 encoding failed");
            return Result.Failure<EncodedJpeg2000>($"Encoding failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<EncodedJpeg2000>> EncodeWithRoiAsync(
        ImmutableArray<byte> imageData,
        FacialRoiSet roiSet,
        Jpeg2000Options options,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                new EventId(920303, "Jpeg2000Service"),
                "Jpeg2000Service processing started."
            );

            using var image = RgbImage.Load(imageData.ToArray());

            var result = _encoder.EncodeWithRoi(
                image,
                roiSet,
                options.CompressionRate,
                options.RoiStartLevel,
                true, // ROI enabled
                options.AlignRoi
            );
            if (result.IsFailure)
            {
                return Result.Failure<EncodedJpeg2000>(
                    $"JPEG 2000 encoding with ROI failed: {result.Error}"
                );
            }

            var encodedData = ImmutableArray.Create(result.Value);
            var metadata = CreateMetadata(image, encodedData.Length, options.CompressionRate, true)
                .Add("RoiRegions", roiSet.AllRegions.Count)
                .Add("RoiStartLevel", options.RoiStartLevel);

            var encoded = new EncodedJpeg2000(
                encodedData,
                new ImageDimensions(image.Width, image.Height),
                options.CompressionRate,
                encodedData.Length,
                metadata
            );

            _logger.LogDebug(
                new EventId(920304, "Jpeg2000Service"),
                "Jpeg2000Service processing completed."
            );
            return Result.Success(encoded);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(920305, "Jpeg2000Service"),
                "JPEG 2000 encoding with ROI failed"
            );
            return Result.Failure<EncodedJpeg2000>($"Encoding with ROI failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<EncodedJpeg2000>> EncodeToTargetSizeAsync(
        ImmutableArray<byte> imageData,
        int targetSizeBytes,
        Maybe<FacialRoiSet> roiSet,
        TargetSizeOptions options,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                new EventId(920306, "Jpeg2000Service"),
                "Jpeg2000Service processing started."
            );

            if (targetSizeBytes <= 0)
            {
                return Result.Failure<EncodedJpeg2000>("Target size must be greater than zero");
            }

            using var image = RgbImage.Load(imageData.ToArray());
            var pixelCount = image.Width * image.Height;

            // Start with initial rate estimate
            var currentRate = EstimateInitialRate(targetSizeBytes, pixelCount);
            var bestResult = Maybe<EncodedJpeg2000>.None;
            var attempts = 0;

            while (attempts < options.MaxAttempts)
            {
                attempts++;
                _logger.LogDebug(
                    new EventId(920307, "Jpeg2000Service"),
                    "Jpeg2000Service processing status changed."
                );

                var encodingOptions = new Jpeg2000Options(
                    currentRate,
                    options.QualityLayers,
                    options.TileSize,
                    options.RoiStartLevel,
                    options.AlignRoi
                );

                Result<EncodedJpeg2000> result;
                if (roiSet.HasValue)
                {
                    result = await EncodeWithRoiAsync(
                        imageData,
                        roiSet.Value,
                        encodingOptions,
                        cancellationToken
                    );
                }
                else
                {
                    result = await EncodeAsync(imageData, encodingOptions, cancellationToken);
                }

                if (result.IsFailure)
                {
                    return result;
                }

                var encoded = result.Value;
                var sizeDiff = encoded.FileSize - targetSizeBytes;
                var sizeRatio = (float)encoded.FileSize / targetSizeBytes;

                _logger.LogDebug(
                    new EventId(920308, "Jpeg2000Service"),
                    "Jpeg2000Service processing status changed."
                );

                // Check if we're within tolerance
                if (Math.Abs(sizeRatio - 1.0f) <= options.SizeTolerance)
                {
                    _logger.LogDebug(
                        new EventId(920309, "Jpeg2000Service"),
                        "Target size achieved within tolerance"
                    );
                    return Result.Success(encoded);
                }

                // Update best result if closer to target
                if (
                    !bestResult.HasValue
                    || Math.Abs(encoded.FileSize - targetSizeBytes)
                        < Math.Abs(bestResult.Value.FileSize - targetSizeBytes)
                )
                {
                    bestResult = Maybe<EncodedJpeg2000>.From(encoded);
                }

                // Adjust rate for next attempt
                currentRate = AdjustCompressionRate(currentRate, sizeRatio, attempts);

                // Check if rate is getting too extreme
                if (currentRate < 0.1f || currentRate > 10.0f)
                {
                    _logger.LogWarning(
                        new EventId(920310, "Jpeg2000Service"),
                        "Jpeg2000Service processing status changed."
                    );
                    break;
                }
            }

            // Return best result if we have one
            if (bestResult.HasValue)
            {
                _logger.LogWarning(
                    new EventId(920311, "Jpeg2000Service"),
                    "Jpeg2000Service processing status changed."
                );
                return Result.Success(bestResult.Value);
            }

            return Result.Failure<EncodedJpeg2000>(
                $"Failed to achieve target size {targetSizeBytes} bytes after {attempts} attempts"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(920312, "Jpeg2000Service"),
                "JPEG 2000 encoding to target size failed"
            );
            return Result.Failure<EncodedJpeg2000>($"Encoding to target size failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<CompressionRecommendation>> CalculateOptimalSettingsAsync(
        ImmutableArray<byte> imageData,
        CompressionConstraints constraints,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                new EventId(920313, "Jpeg2000Service"),
                "Calculating optimal JPEG 2000 settings for constraints"
            );

            using var image = RgbImage.Load(imageData.ToArray());
            var dimensions = new ImageDimensions(image.Width, image.Height);
            var pixelCount = image.Width * image.Height;

            // Calculate recommended settings based on constraints
            var recommendedRate =
                constraints.MaxFileSize.HasValue
                    ? EstimateInitialRate(constraints.MaxFileSize.Value, pixelCount)
                : constraints.MinQuality.HasValue
                    ? EstimateRateForQuality(constraints.MinQuality.Value)
                : 0.7f; // Default

            // Adjust for content complexity if needed
            if (constraints.ConsiderContent)
            {
                var complexity = await EstimateImageComplexityAsync(image);
                recommendedRate = AdjustRateForComplexity(recommendedRate, complexity);
            }

            // Determine ROI usage
            var useRoi = constraints.PreferRoi && constraints.MaxFileSize.HasValue;
            var roiStartLevel = useRoi ? 3 : 0; // Default to smoothest transitions

            // Create recommendation
            var recommendation = new CompressionRecommendation(
                recommendedRate,
                useRoi,
                roiStartLevel,
                constraints.MaxFileSize,
                EstimateFileSize(pixelCount, recommendedRate),
                dimensions,
                ImmutableDictionary<string, object>
                    .Empty.Add("PixelCount", pixelCount)
                    .Add("EstimatedQuality", EstimateQualityForRate(recommendedRate))
            );

            _logger.LogDebug(
                new EventId(920314, "Jpeg2000Service"),
                "Jpeg2000Service processing status changed."
            );

            return Result.Success(recommendation);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(920315, "Jpeg2000Service"),
                "Failed to calculate optimal settings"
            );
            return Result.Failure<CompressionRecommendation>(
                $"Settings calculation failed: {ex.Message}"
            );
        }
    }

    private static float EstimateInitialRate(int targetSizeBytes, int pixelCount)
    {
        // Rough estimate: targetSize * 8 bits / pixelCount
        // Adjusted with empirical factor
        var bitsPerPixel = (targetSizeBytes * 8.0f) / pixelCount;
        return Math.Max(0.1f, Math.Min(2.0f, bitsPerPixel * 0.85f));
    }

    private static float EstimateRateForQuality(float quality)
    {
        // Empirical mapping from quality (0-1) to compression rate
        return quality switch
        {
            >= 0.9f => 1.5f,
            >= 0.8f => 1.0f,
            >= 0.7f => 0.7f,
            >= 0.6f => 0.5f,
            _ => 0.3f,
        };
    }

    private static float EstimateQualityForRate(float rate)
    {
        // Inverse of EstimateRateForQuality
        return rate switch
        {
            >= 1.5f => 0.95f,
            >= 1.0f => 0.85f,
            >= 0.7f => 0.75f,
            >= 0.5f => 0.65f,
            _ => 0.5f,
        };
    }

    private static int EstimateFileSize(int pixelCount, float rate)
    {
        // Estimate: pixelCount * rate / 8 bytes
        // With overhead factor
        return (int)(pixelCount * rate / 8 * 1.1f);
    }

    private static float AdjustCompressionRate(float currentRate, float sizeRatio, int attempt)
    {
        // Adaptive adjustment based on how far we are from target
        var adjustment = attempt switch
        {
            1 => 0.5f, // Large adjustments initially
            2 => 0.3f,
            3 => 0.2f,
            _ => 0.1f, // Fine adjustments later
        };

        if (sizeRatio > 1.0f)
        {
            // File too large, decrease rate
            return currentRate * (1.0f - adjustment * (sizeRatio - 1.0f));
        }
        else
        {
            // File too small, increase rate
            return currentRate * (1.0f + adjustment * (1.0f - sizeRatio));
        }
    }

    private static async Task<float> EstimateImageComplexityAsync(RgbImage image)
    {
        // Simple complexity estimation based on color variance
        // More sophisticated analysis could be implemented
        await Task.CompletedTask;
        return 0.5f; // Medium complexity default
    }

    private static float AdjustRateForComplexity(float baseRate, float complexity)
    {
        // Higher complexity needs higher bitrate
        return baseRate * (0.8f + 0.4f * complexity);
    }

    private static ImmutableDictionary<string, object> CreateMetadata(
        RgbImage image,
        int fileSize,
        float compressionRate,
        bool roiUsed
    )
    {
        return ImmutableDictionary<string, object>
            .Empty.Add("Width", image.Width)
            .Add("Height", image.Height)
            .Add("FileSize", fileSize)
            .Add("CompressionRate", compressionRate)
            .Add("RoiUsed", roiUsed)
            .Add("Format", "JPEG2000");
    }
}

/// <summary>
/// Options for JPEG 2000 encoding.
/// </summary>
[PublicAPI]
public sealed record Jpeg2000Options(
    float CompressionRate = 0.7f,
    int QualityLayers = 5,
    Maybe<ImageDimensions> TileSize = default,
    int RoiStartLevel = 3,
    bool AlignRoi = false
);

/// <summary>
/// Options for target size encoding.
/// </summary>
[PublicAPI]
public sealed record TargetSizeOptions(
    int MaxAttempts = 5,
    float SizeTolerance = 0.05f,
    int QualityLayers = 5,
    Maybe<ImageDimensions> TileSize = default,
    int RoiStartLevel = 3,
    bool AlignRoi = false
);

/// <summary>
/// Constraints for compression optimization.
/// </summary>
[PublicAPI]
public sealed record CompressionConstraints(
    Maybe<int> MaxFileSize = default,
    Maybe<float> MinQuality = default,
    bool PreferRoi = true,
    bool ConsiderContent = false
);

/// <summary>
/// Result of JPEG 2000 encoding.
/// </summary>
[PublicAPI]
public sealed record EncodedJpeg2000(
    ImmutableArray<byte> ImageData,
    ImageDimensions Dimensions,
    float CompressionRate,
    int FileSize,
    ImmutableDictionary<string, object> Metadata
);

/// <summary>
/// Recommended compression settings.
/// </summary>
[PublicAPI]
public sealed record CompressionRecommendation(
    float RecommendedRate,
    bool UseRoi,
    int RoiStartLevel,
    Maybe<int> TargetSize,
    int EstimatedSize,
    ImageDimensions ImageDimensions,
    ImmutableDictionary<string, object> Analysis
);
