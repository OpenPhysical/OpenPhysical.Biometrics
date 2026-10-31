using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Domain.Transformations;

namespace OpenPhysical.Biometrics.Core.Configuration;

/// <summary>
/// Centralized configuration for biometric processing with sensible defaults.
/// Eliminates hardcoded values throughout the codebase.
/// </summary>
[PublicAPI]
public sealed record BiometricConfiguration
{
    /// <summary>
    /// Default configuration with industry-standard values
    /// </summary>
    public static BiometricConfiguration Default => new();

    /// <summary>
    /// Face detection configuration
    /// </summary>
    public FaceDetectionConfiguration FaceDetection { get; init; } = new();

    /// <summary>
    /// Image processing configuration
    /// </summary>
    public ImageProcessingConfiguration ImageProcessing { get; init; } = new();

    /// <summary>
    /// Encoding configuration for different formats
    /// </summary>
    public EncodingConfiguration Encoding { get; init; } = new();

    /// <summary>
    /// Quality assessment configuration
    /// </summary>
    public QualityConfiguration Quality { get; init; } = new();

    /// <summary>
    /// Standard preset configurations for different card types
    /// </summary>
    public StandardPresetsConfiguration Standards { get; init; } = new();

    /// <summary>
    /// Processing performance and timeout configuration
    /// </summary>
    public PerformanceConfiguration Performance { get; init; } = new();

    /// <summary>
    /// Validation configuration for input and output constraints
    /// </summary>
    public ValidationConfiguration Validation { get; init; } = new();
}

/// <summary>
/// Face detection specific configuration
/// </summary>
[PublicAPI]
public sealed record FaceDetectionConfiguration
{
    /// <summary>
    /// Minimum confidence threshold for face detection (0.0 to 1.0)
    /// </summary>
    public float MinConfidence { get; init; } = 0.8f;

    /// <summary>
    /// Strict mode minimum confidence threshold
    /// </summary>
    public float StrictModeMinConfidence { get; init; } = 0.85f;

    /// <summary>
    /// High quality minimum confidence threshold for archival processing
    /// </summary>
    public float ArchivalMinConfidence { get; init; } = 0.95f;

    /// <summary>
    /// Relaxed minimum confidence for batch processing
    /// </summary>
    public float RelaxedMinConfidence { get; init; } = 0.7f;

    /// <summary>
    /// Whether to require exactly one face in the image
    /// </summary>
    public bool RequireSingleFace { get; init; } = true;

    /// <summary>
    /// Maximum rotation angle allowed for face alignment (degrees)
    /// </summary>
    public float MaxRotationDegrees { get; init; } = 15.0f;
}

/// <summary>
/// Image processing configuration
/// </summary>
[PublicAPI]
public sealed record ImageProcessingConfiguration
{
    /// <summary>
    /// Standard PIV image dimensions
    /// </summary>
    public ImageDimensions PivDimensions { get; init; } = new(420, 560);

    /// <summary>
    /// Standard ICAO dimensions for passport photos
    /// </summary>
    public ImageDimensions IcaoDimensions { get; init; } = new(413, 531);

    /// <summary>
    /// Whether to preserve EXIF metadata in output images
    /// </summary>
    public bool PreserveMetadata { get; init; } = false;

    /// <summary>
    /// Default aspect ratio tolerance for face detection
    /// </summary>
    public float AspectRatioTolerance { get; init; } = 0.1f;

    /// <summary>
    /// Default margin ratio for ROI expansion
    /// </summary>
    public float DefaultMarginRatio { get; init; } = 0.1f;
}

/// <summary>
/// Encoding configuration for different formats
/// </summary>
[PublicAPI]
public sealed record EncodingConfiguration
{
    /// <summary>
    /// JPEG 2000 encoding configuration
    /// </summary>
    public Jpeg2000Configuration Jpeg2000 { get; init; } = new();

    /// <summary>
    /// JPEG encoding configuration
    /// </summary>
    public JpegConfiguration Jpeg { get; init; } = new();

    /// <summary>
    /// Default encoding format
    /// </summary>
    public string DefaultFormat { get; init; } = "jp2";
}

/// <summary>
/// JPEG 2000 specific configuration
/// </summary>
[PublicAPI]
public sealed record Jpeg2000Configuration
{
    /// <summary>
    /// Default compression rate in bits per pixel
    /// </summary>
    public float DefaultRate { get; init; } = 0.7f;

    /// <summary>
    /// High quality compression rate
    /// </summary>
    public float HighQualityRate { get; init; } = 1.0f;

    /// <summary>
    /// Archival quality compression rate
    /// </summary>
    public float ArchivalRate { get; init; } = 4.0f;

    /// <summary>
    /// Default ROI start level (0=aggressive, 3=smoothest)
    /// </summary>
    public int DefaultRoiStartLevel { get; init; } = 3;

    /// <summary>
    /// Conservative ROI start level for high quality
    /// </summary>
    public int ConservativeRoiStartLevel { get; init; } = 2;

    /// <summary>
    /// Aggressive ROI start level for maximum compression
    /// </summary>
    public int AggressiveRoiStartLevel { get; init; } = 0;

    /// <summary>
    /// Whether to enable ROI encoding by default
    /// </summary>
    public bool EnableRoiByDefault { get; init; } = true;

    /// <summary>
    /// Whether to align ROI regions with compression blocks
    /// </summary>
    public bool AlignRoiByDefault { get; init; } = false;
}

/// <summary>
/// JPEG encoding configuration
/// </summary>
[PublicAPI]
public sealed record JpegConfiguration
{
    /// <summary>
    /// Default JPEG quality (1-100)
    /// </summary>
    public int DefaultQuality { get; init; } = 85;

    /// <summary>
    /// High quality JPEG setting
    /// </summary>
    public int HighQuality { get; init; } = 95;

    /// <summary>
    /// Standard quality for most use cases
    /// </summary>
    public int StandardQuality { get; init; } = 85;

    /// <summary>
    /// Minimum acceptable quality
    /// </summary>
    public int MinQuality { get; init; } = 70;
}

/// <summary>
/// Quality assessment configuration
/// </summary>
[PublicAPI]
public sealed record QualityConfiguration
{
    /// <summary>
    /// Default quality threshold for processing
    /// </summary>
    public float DefaultThreshold { get; init; } = 0.7f;

    /// <summary>
    /// High quality threshold for strict processing
    /// </summary>
    public float HighThreshold { get; init; } = 0.8f;

    /// <summary>
    /// Very high quality threshold for premium processing
    /// </summary>
    public float VeryHighThreshold { get; init; } = 0.85f;

    /// <summary>
    /// Archival quality threshold for long-term storage
    /// </summary>
    public float ArchivalThreshold { get; init; } = 0.9f;

    /// <summary>
    /// Minimum acceptable quality threshold
    /// </summary>
    public float MinThreshold { get; init; } = 0.5f;

    /// <summary>
    /// Whether to enforce compliance by default
    /// </summary>
    public bool EnforceComplianceByDefault { get; init; } = false;
}

/// <summary>
/// Standard preset configurations for different card types and use cases
/// </summary>
[PublicAPI]
public sealed record StandardPresetsConfiguration
{
    /// <summary>
    /// TWIC card maximum size constraints
    /// </summary>
    public TargetSizePreset TwicMax { get; init; } = new(14000, "TWIC card maximum compatibility");

    /// <summary>
    /// PIV card minimum size for cards with limited space
    /// </summary>
    public TargetSizePreset PivMin { get; init; } =
        new(12000, "PIV card minimum space compatibility");

    /// <summary>
    /// PIV balanced quality/size ratio (recommended default)
    /// </summary>
    public TargetSizePreset PivBalanced { get; init; } =
        new(22000, "PIV card balanced quality/size");

    /// <summary>
    /// PIV high quality for enhanced detail
    /// </summary>
    public TargetSizePreset PivHigh { get; init; } = new(30000, "PIV card high quality");

    /// <summary>
    /// PIV very high quality for premium applications
    /// </summary>
    public TargetSizePreset PivVeryHigh { get; init; } = new(50000, "PIV card very high quality");

    /// <summary>
    /// Minimal size for storage-constrained environments
    /// </summary>
    public TargetSizePreset Minimal { get; init; } = new(10000, "Minimal storage footprint");
}

/// <summary>
/// Target size preset with description
/// </summary>
[PublicAPI]
public sealed record TargetSizePreset(int TargetBytes, string Description);

/// <summary>
/// Performance and timeout configuration
/// </summary>
[PublicAPI]
public sealed record PerformanceConfiguration
{
    /// <summary>
    /// Default processing timeout
    /// </summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Fast processing timeout for batch operations
    /// </summary>
    public TimeSpan FastTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Extended timeout for high-quality processing
    /// </summary>
    public TimeSpan ExtendedTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Default number of retry attempts
    /// </summary>
    public int DefaultRetries { get; init; } = 2;

    /// <summary>
    /// Fast processing retries (fail-fast)
    /// </summary>
    public int FastRetries { get; init; } = 1;

    /// <summary>
    /// Extended retries for critical operations
    /// </summary>
    public int ExtendedRetries { get; init; } = 5;

    /// <summary>
    /// Maximum retry attempts allowed
    /// </summary>
    public int MaxRetries { get; init; } = 10;
}

/// <summary>
/// Validation configuration for input and output constraints
/// </summary>
[PublicAPI]
public sealed record ValidationConfiguration
{
    /// <summary>
    /// Minimum acceptable image width
    /// </summary>
    public int MinImageWidth { get; init; } = 100;

    /// <summary>
    /// Minimum acceptable image height
    /// </summary>
    public int MinImageHeight { get; init; } = 100;

    /// <summary>
    /// Maximum acceptable image width
    /// </summary>
    public int MaxImageWidth { get; init; } = 4000;

    /// <summary>
    /// Maximum acceptable image height
    /// </summary>
    public int MaxImageHeight { get; init; } = 4000;

    /// <summary>
    /// Maximum input file size in bytes
    /// </summary>
    public long MaxInputFileSize { get; init; } = 50 * 1024 * 1024; // 50MB

    /// <summary>
    /// Minimum input file size in bytes
    /// </summary>
    public long MinInputFileSize { get; init; } = 1024; // 1KB

    /// <summary>
    /// ROI start level validation range
    /// </summary>
    public (int Min, int Max) RoiStartLevelRange { get; init; } = (0, 3);

    /// <summary>
    /// Face confidence validation range
    /// </summary>
    public (float Min, float Max) ConfidenceRange { get; init; } = (0.0f, 1.0f);

    /// <summary>
    /// JPEG quality validation range
    /// </summary>
    public (int Min, int Max) JpegQualityRange { get; init; } = (1, 100);

    /// <summary>
    /// Quality threshold validation range
    /// </summary>
    public (float Min, float Max) QualityThresholdRange { get; init; } = (0.0f, 1.0f);
}

/// <summary>
/// Image dimensions value object
/// </summary>
[PublicAPI]
public sealed record ImageDimensions(int Width, int Height)
{
    /// <summary>
    /// Gets the aspect ratio of the image
    /// </summary>
    public float AspectRatio => (float)Width / Height;

    /// <summary>
    /// Gets the total pixel count
    /// </summary>
    public int PixelCount => Width * Height;

    /// <summary>
    /// Standard PIV dimensions (FIPS 201-3)
    /// </summary>
    public static ImageDimensions Piv => new(420, 560);

    /// <summary>
    /// Standard ICAO dimensions for passports
    /// </summary>
    public static ImageDimensions Icao => new(413, 531);

    /// <summary>
    /// Square dimensions for ML model input
    /// </summary>
    public static ImageDimensions Square112 => new(112, 112);

    /// <summary>
    /// Validates that dimensions are within acceptable ranges
    /// </summary>
    public bool IsValid(ValidationConfiguration validation) =>
        Width >= validation.MinImageWidth
        && Width <= validation.MaxImageWidth
        && Height >= validation.MinImageHeight
        && Height <= validation.MaxImageHeight;
}

/// <summary>
/// Extension methods for BiometricConfiguration
/// </summary>
[PublicAPI]
public static class BiometricConfigurationExtensions
{
    /// <summary>
    /// Creates default ProcessingOptions from BiometricConfiguration
    /// </summary>
    public static ProcessingOptions GetDefaultProcessingOptions(this BiometricConfiguration config)
    {
        return new ProcessingOptions
        {
            MinFaceConfidence = config.FaceDetection.MinConfidence,
            RequireSingleFace = config.FaceDetection.RequireSingleFace,
            MaxRetries = config.Performance.DefaultRetries,
            ProcessingTimeout = config.Performance.DefaultTimeout,
            PreserveMetadata = config.ImageProcessing.PreserveMetadata,
            RoiStartLevel = config.Encoding.Jpeg2000.DefaultRoiStartLevel,
            EnableRoi = config.Encoding.Jpeg2000.EnableRoiByDefault,
            AlignRoi = config.Encoding.Jpeg2000.AlignRoiByDefault,
            MaxRotationDegrees = config.FaceDetection.MaxRotationDegrees,
            Strategy = EncodingStrategy.TargetSize(config.Standards.PivBalanced.TargetBytes),
        };
    }
}
