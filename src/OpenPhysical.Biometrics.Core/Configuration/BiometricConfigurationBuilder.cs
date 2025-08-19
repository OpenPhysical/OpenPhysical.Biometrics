using JetBrains.Annotations;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Core.Configuration;

/// <summary>
/// Builder for creating BiometricConfiguration instances with validation
/// </summary>
[PublicAPI]
public sealed class BiometricConfigurationBuilder
{
    private FaceDetectionConfiguration _faceDetection = new();
    private ImageProcessingConfiguration _imageProcessing = new();
    private EncodingConfiguration _encoding = new();
    private QualityConfiguration _quality = new();
    private StandardPresetsConfiguration _standards = new();
    private PerformanceConfiguration _performance = new();
    private ValidationConfiguration _validation = new();

    /// <summary>
    /// Creates a new builder with default values
    /// </summary>
    public static BiometricConfigurationBuilder Create() => new();

    /// <summary>
    /// Creates a builder from an existing configuration
    /// </summary>
    public static BiometricConfigurationBuilder FromConfiguration(BiometricConfiguration config) =>
        new()
        {
            _faceDetection = config.FaceDetection,
            _imageProcessing = config.ImageProcessing,
            _encoding = config.Encoding,
            _quality = config.Quality,
            _standards = config.Standards,
            _performance = config.Performance,
            _validation = config.Validation
        };

    /// <summary>
    /// Configures face detection settings
    /// </summary>
    public BiometricConfigurationBuilder WithFaceDetection(Func<FaceDetectionConfiguration, FaceDetectionConfiguration> configure)
    {
        _faceDetection = configure(_faceDetection);
        return this;
    }

    /// <summary>
    /// Configures image processing settings
    /// </summary>
    public BiometricConfigurationBuilder WithImageProcessing(Func<ImageProcessingConfiguration, ImageProcessingConfiguration> configure)
    {
        _imageProcessing = configure(_imageProcessing);
        return this;
    }

    /// <summary>
    /// Configures encoding settings
    /// </summary>
    public BiometricConfigurationBuilder WithEncoding(Func<EncodingConfiguration, EncodingConfiguration> configure)
    {
        _encoding = configure(_encoding);
        return this;
    }

    /// <summary>
    /// Configures quality assessment settings
    /// </summary>
    public BiometricConfigurationBuilder WithQuality(Func<QualityConfiguration, QualityConfiguration> configure)
    {
        _quality = configure(_quality);
        return this;
    }

    /// <summary>
    /// Configures standard presets
    /// </summary>
    public BiometricConfigurationBuilder WithStandards(Func<StandardPresetsConfiguration, StandardPresetsConfiguration> configure)
    {
        _standards = configure(_standards);
        return this;
    }

    /// <summary>
    /// Configures performance settings
    /// </summary>
    public BiometricConfigurationBuilder WithPerformance(Func<PerformanceConfiguration, PerformanceConfiguration> configure)
    {
        _performance = configure(_performance);
        return this;
    }

    /// <summary>
    /// Configures validation settings
    /// </summary>
    public BiometricConfigurationBuilder WithValidation(Func<ValidationConfiguration, ValidationConfiguration> configure)
    {
        _validation = configure(_validation);
        return this;
    }

    /// <summary>
    /// Sets face detection minimum confidence
    /// </summary>
    public BiometricConfigurationBuilder WithMinFaceConfidence(float confidence)
    {
        _faceDetection = _faceDetection with { MinConfidence = confidence };
        return this;
    }

    /// <summary>
    /// Sets default JPEG 2000 compression rate
    /// </summary>
    public BiometricConfigurationBuilder WithDefaultCompressionRate(float rate)
    {
        _encoding = _encoding with 
        { 
            Jpeg2000 = _encoding.Jpeg2000 with { DefaultRate = rate } 
        };
        return this;
    }

    /// <summary>
    /// Sets default processing timeout
    /// </summary>
    public BiometricConfigurationBuilder WithTimeout(TimeSpan timeout)
    {
        _performance = _performance with { DefaultTimeout = timeout };
        return this;
    }

    /// <summary>
    /// Sets target size for a specific preset
    /// </summary>
    public BiometricConfigurationBuilder WithPivBalancedSize(int targetBytes)
    {
        _standards = _standards with 
        { 
            PivBalanced = new TargetSizePreset(targetBytes, _standards.PivBalanced.Description) 
        };
        return this;
    }

    /// <summary>
    /// Enables strict mode with higher confidence thresholds
    /// </summary>
    public BiometricConfigurationBuilder WithStrictMode()
    {
        _faceDetection = _faceDetection with 
        { 
            MinConfidence = _faceDetection.StrictModeMinConfidence 
        };
        _quality = _quality with 
        { 
            DefaultThreshold = _quality.VeryHighThreshold 
        };
        return this;
    }

    /// <summary>
    /// Configures for fast batch processing
    /// </summary>
    public BiometricConfigurationBuilder WithFastProcessing()
    {
        _performance = _performance with 
        { 
            DefaultTimeout = _performance.FastTimeout,
            DefaultRetries = _performance.FastRetries
        };
        _faceDetection = _faceDetection with 
        { 
            MinConfidence = _faceDetection.RelaxedMinConfidence 
        };
        return this;
    }

    /// <summary>
    /// Configures for archival quality processing
    /// </summary>
    public BiometricConfigurationBuilder WithArchivalQuality()
    {
        _faceDetection = _faceDetection with 
        { 
            MinConfidence = _faceDetection.ArchivalMinConfidence 
        };
        _encoding = _encoding with 
        { 
            Jpeg2000 = _encoding.Jpeg2000 with { DefaultRate = _encoding.Jpeg2000.ArchivalRate } 
        };
        _quality = _quality with 
        { 
            DefaultThreshold = _quality.ArchivalThreshold 
        };
        _imageProcessing = _imageProcessing with 
        { 
            PreserveMetadata = true 
        };
        _performance = _performance with 
        { 
            DefaultTimeout = _performance.ExtendedTimeout 
        };
        return this;
    }

    /// <summary>
    /// Builds and validates the configuration
    /// </summary>
    public Result<BiometricConfiguration> Build()
    {
        var config = new BiometricConfiguration
        {
            FaceDetection = _faceDetection,
            ImageProcessing = _imageProcessing,
            Encoding = _encoding,
            Quality = _quality,
            Standards = _standards,
            Performance = _performance,
            Validation = _validation
        };

        return ValidateConfiguration(config)
            .Map(() => config);
    }

    /// <summary>
    /// Builds the configuration without validation (use with caution)
    /// </summary>
    public BiometricConfiguration BuildUnsafe() =>
        new()
        {
            FaceDetection = _faceDetection,
            ImageProcessing = _imageProcessing,
            Encoding = _encoding,
            Quality = _quality,
            Standards = _standards,
            Performance = _performance,
            Validation = _validation
        };

    private Result ValidateConfiguration(BiometricConfiguration config)
    {
        // Validate face detection configuration
        if (!config.Validation.ConfidenceRange.IsInRange(config.FaceDetection.MinConfidence))
            return Result.Failure($"MinConfidence {config.FaceDetection.MinConfidence} is outside valid range {config.Validation.ConfidenceRange}");

        if (!config.Validation.ConfidenceRange.IsInRange(config.FaceDetection.StrictModeMinConfidence))
            return Result.Failure($"StrictModeMinConfidence {config.FaceDetection.StrictModeMinConfidence} is outside valid range {config.Validation.ConfidenceRange}");

        if (!config.Validation.ConfidenceRange.IsInRange(config.FaceDetection.ArchivalMinConfidence))
            return Result.Failure($"ArchivalMinConfidence {config.FaceDetection.ArchivalMinConfidence} is outside valid range {config.Validation.ConfidenceRange}");

        if (!config.Validation.ConfidenceRange.IsInRange(config.FaceDetection.RelaxedMinConfidence))
            return Result.Failure($"RelaxedMinConfidence {config.FaceDetection.RelaxedMinConfidence} is outside valid range {config.Validation.ConfidenceRange}");

        // Validate ROI start levels
        if (!config.Validation.RoiStartLevelRange.IsInRange(config.Encoding.Jpeg2000.DefaultRoiStartLevel))
            return Result.Failure($"DefaultRoiStartLevel {config.Encoding.Jpeg2000.DefaultRoiStartLevel} is outside valid range {config.Validation.RoiStartLevelRange}");

        if (!config.Validation.RoiStartLevelRange.IsInRange(config.Encoding.Jpeg2000.ConservativeRoiStartLevel))
            return Result.Failure($"ConservativeRoiStartLevel {config.Encoding.Jpeg2000.ConservativeRoiStartLevel} is outside valid range {config.Validation.RoiStartLevelRange}");

        if (!config.Validation.RoiStartLevelRange.IsInRange(config.Encoding.Jpeg2000.AggressiveRoiStartLevel))
            return Result.Failure($"AggressiveRoiStartLevel {config.Encoding.Jpeg2000.AggressiveRoiStartLevel} is outside valid range {config.Validation.RoiStartLevelRange}");

        // Validate JPEG quality settings
        if (!config.Validation.JpegQualityRange.IsInRange(config.Encoding.Jpeg.DefaultQuality))
            return Result.Failure($"JPEG DefaultQuality {config.Encoding.Jpeg.DefaultQuality} is outside valid range {config.Validation.JpegQualityRange}");

        if (!config.Validation.JpegQualityRange.IsInRange(config.Encoding.Jpeg.HighQuality))
            return Result.Failure($"JPEG HighQuality {config.Encoding.Jpeg.HighQuality} is outside valid range {config.Validation.JpegQualityRange}");

        if (!config.Validation.JpegQualityRange.IsInRange(config.Encoding.Jpeg.StandardQuality))
            return Result.Failure($"JPEG StandardQuality {config.Encoding.Jpeg.StandardQuality} is outside valid range {config.Validation.JpegQualityRange}");

        if (!config.Validation.JpegQualityRange.IsInRange(config.Encoding.Jpeg.MinQuality))
            return Result.Failure($"JPEG MinQuality {config.Encoding.Jpeg.MinQuality} is outside valid range {config.Validation.JpegQualityRange}");

        // Validate quality thresholds
        if (!config.Validation.QualityThresholdRange.IsInRange(config.Quality.DefaultThreshold))
            return Result.Failure($"Quality DefaultThreshold {config.Quality.DefaultThreshold} is outside valid range {config.Validation.QualityThresholdRange}");

        if (!config.Validation.QualityThresholdRange.IsInRange(config.Quality.HighThreshold))
            return Result.Failure($"Quality HighThreshold {config.Quality.HighThreshold} is outside valid range {config.Validation.QualityThresholdRange}");

        if (!config.Validation.QualityThresholdRange.IsInRange(config.Quality.VeryHighThreshold))
            return Result.Failure($"Quality VeryHighThreshold {config.Quality.VeryHighThreshold} is outside valid range {config.Validation.QualityThresholdRange}");

        if (!config.Validation.QualityThresholdRange.IsInRange(config.Quality.ArchivalThreshold))
            return Result.Failure($"Quality ArchivalThreshold {config.Quality.ArchivalThreshold} is outside valid range {config.Validation.QualityThresholdRange}");

        if (!config.Validation.QualityThresholdRange.IsInRange(config.Quality.MinThreshold))
            return Result.Failure($"Quality MinThreshold {config.Quality.MinThreshold} is outside valid range {config.Validation.QualityThresholdRange}");

        // Validate image dimensions
        if (!config.ImageProcessing.PivDimensions.IsValid(config.Validation))
            return Result.Failure($"PIV dimensions {config.ImageProcessing.PivDimensions} are outside valid range");

        if (!config.ImageProcessing.IcaoDimensions.IsValid(config.Validation))
            return Result.Failure($"ICAO dimensions {config.ImageProcessing.IcaoDimensions} are outside valid range");

        // Validate performance settings
        if (config.Performance.DefaultTimeout <= TimeSpan.Zero)
            return Result.Failure("DefaultTimeout must be positive");

        if (config.Performance.FastTimeout <= TimeSpan.Zero)
            return Result.Failure("FastTimeout must be positive");

        if (config.Performance.ExtendedTimeout <= TimeSpan.Zero)
            return Result.Failure("ExtendedTimeout must be positive");

        if (config.Performance.DefaultRetries < 0)
            return Result.Failure("DefaultRetries must be non-negative");

        if (config.Performance.FastRetries < 0)
            return Result.Failure("FastRetries must be non-negative");

        if (config.Performance.ExtendedRetries < 0)
            return Result.Failure("ExtendedRetries must be non-negative");

        if (config.Performance.MaxRetries < 0)
            return Result.Failure("MaxRetries must be non-negative");

        // Validate preset target sizes
        if (config.Standards.TwicMax.TargetBytes <= 0)
            return Result.Failure("TWIC max target size must be positive");

        if (config.Standards.PivMin.TargetBytes <= 0)
            return Result.Failure("PIV min target size must be positive");

        if (config.Standards.PivBalanced.TargetBytes <= 0)
            return Result.Failure("PIV balanced target size must be positive");

        if (config.Standards.PivHigh.TargetBytes <= 0)
            return Result.Failure("PIV high target size must be positive");

        if (config.Standards.PivVeryHigh.TargetBytes <= 0)
            return Result.Failure("PIV very high target size must be positive");

        if (config.Standards.Minimal.TargetBytes <= 0)
            return Result.Failure("Minimal target size must be positive");

        return Result.Success();
    }
}

/// <summary>
/// Extension methods for range validation
/// </summary>
internal static class RangeExtensions
{
    public static bool IsInRange<T>(this (T Min, T Max) range, T value) where T : IComparable<T> =>
        value.CompareTo(range.Min) >= 0 && value.CompareTo(range.Max) <= 0;
}