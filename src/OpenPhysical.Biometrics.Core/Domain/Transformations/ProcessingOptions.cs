using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Configuration;

namespace OpenPhysical.Biometrics.Core.Domain.Transformations;

/// <summary>
/// Processing options for face image transformation and encoding
/// </summary>
[PublicAPI]
public sealed record ProcessingOptions
{
    /// <summary>
    /// Gets the minimum confidence threshold for face detection
    /// </summary>
    /// <value>Confidence value from 0.0 to 1.0. Default from BiometricConfiguration.</value>
    public float MinFaceConfidence { get; init; } =
        BiometricConfiguration.Default.FaceDetection.MinConfidence;

    /// <summary>
    /// Gets a value indicating whether to require exactly one face in the image
    /// </summary>
    /// <value>True to fail if multiple faces are detected; false to use the highest confidence face. Default from BiometricConfiguration.</value>
    public bool RequireSingleFace { get; init; } =
        BiometricConfiguration.Default.FaceDetection.RequireSingleFace;

    /// <summary>
    /// Gets the maximum number of retry attempts for processing
    /// </summary>
    /// <value>Number of retry attempts. Default from BiometricConfiguration.</value>
    public int MaxRetries { get; init; } =
        BiometricConfiguration.Default.Performance.DefaultRetries;

    /// <summary>
    /// Gets the timeout for processing operations
    /// </summary>
    /// <value>Maximum time allowed for processing. Default from BiometricConfiguration.</value>
    public TimeSpan ProcessingTimeout { get; init; } =
        BiometricConfiguration.Default.Performance.DefaultTimeout;

    /// <summary>
    /// Gets a value indicating whether to preserve EXIF metadata in the output image
    /// </summary>
    /// <value>True to preserve metadata; false to strip it. Default from BiometricConfiguration.</value>
    public bool PreserveMetadata { get; init; } =
        BiometricConfiguration.Default.ImageProcessing.PreserveMetadata;

    /// <summary>
    /// Gets the ROI resolution level priority setting
    /// </summary>
    /// <value>
    /// Controls which resolution levels get ROI priority.
    /// 0 = aggressive ROI priority, 1 = balanced, 2 = conservative, 3 = smoothest transitions.
    /// Default from BiometricConfiguration.
    /// </value>
    public int RoiStartLevel { get; init; } =
        BiometricConfiguration.Default.Encoding.Jpeg2000.DefaultRoiStartLevel;

    /// <summary>
    /// Gets a value indicating whether to enable ROI encoding
    /// </summary>
    /// <value>True to enable ROI encoding for enhanced facial quality. Default from BiometricConfiguration.</value>
    public bool EnableRoi { get; init; } =
        BiometricConfiguration.Default.Encoding.Jpeg2000.EnableRoiByDefault;

    /// <summary>
    /// Gets a value indicating whether to align ROI regions with compression blocks
    /// </summary>
    /// <value>True to align ROI regions; false for smoother quality transitions. Default from BiometricConfiguration.</value>
    public bool AlignRoi { get; init; } =
        BiometricConfiguration.Default.Encoding.Jpeg2000.AlignRoiByDefault;

    /// <summary>
    /// Gets the maximum rotation angle allowed for face alignment
    /// </summary>
    /// <value>Maximum rotation in degrees. Default from BiometricConfiguration.</value>
    public float MaxRotationDegrees { get; init; } =
        BiometricConfiguration.Default.FaceDetection.MaxRotationDegrees;

    /// <summary>
    /// Gets the encoding strategy for JPEG 2000 compression
    /// </summary>
    /// <value>Strategy determining how the image is compressed. Default from BiometricConfiguration.</value>
    public EncodingStrategy Strategy { get; init; } =
        EncodingStrategy.FixedRate(BiometricConfiguration.Default.Encoding.Jpeg2000.DefaultRate);

    /// <summary>
    /// TWIC maximum: Target for TWIC card compatibility
    /// </summary>
    /// <value>
    /// Processing options optimized for Transportation Worker Identification Credential (TWIC) cards.
    /// Target size from BiometricConfiguration to fit within card storage constraints.
    /// </value>
    public static ProcessingOptions TwicMax =>
        BiometricConfiguration.Default.GetDefaultProcessingOptions() with
        {
            Strategy = EncodingStrategy.TargetSize(
                BiometricConfiguration.Default.Standards.TwicMax.TargetBytes
            ),
        };

    /// <summary>
    /// PIV minimum: Target for cards with minimum available space
    /// </summary>
    /// <value>
    /// Processing options for PIV cards with minimal available space for facial images.
    /// Target size from BiometricConfiguration to ensure compatibility with any PIV card.
    /// </value>
    public static ProcessingOptions PivMin =>
        BiometricConfiguration.Default.GetDefaultProcessingOptions() with
        {
            Strategy = EncodingStrategy.TargetSize(
                BiometricConfiguration.Default.Standards.PivMin.TargetBytes
            ),
        };

    /// <summary>
    /// PIV balanced: Target for optimal quality/size balance
    /// </summary>
    /// <value>
    /// Balanced processing options for Personal Identity Verification (PIV) cards.
    /// Target size from BiometricConfiguration with optimal balance of quality and compression.
    /// Recommended for most PIV applications.
    /// </value>
    public static ProcessingOptions PivBalanced =>
        BiometricConfiguration.Default.GetDefaultProcessingOptions();

    /// <summary>
    /// PIV high quality: Target for enhanced PIV compatibility
    /// </summary>
    /// <value>
    /// High quality processing options for PIV cards where file size constraints allow.
    /// Target size from BiometricConfiguration with enhanced facial detail preservation.
    /// </value>
    public static ProcessingOptions PivHigh =>
        BiometricConfiguration.Default.GetDefaultProcessingOptions() with
        {
            Strategy = EncodingStrategy.TargetSize(
                BiometricConfiguration.Default.Standards.PivHigh.TargetBytes
            ),
        };

    /// <summary>
    /// PIV very high quality: Target for premium quality
    /// </summary>
    /// <value>
    /// Very high quality processing options with target size from BiometricConfiguration.
    /// Provides excellent facial detail preservation while maintaining reasonable file sizes.
    /// Suitable for applications requiring superior image quality without archival requirements.
    /// </value>
    public static ProcessingOptions PivVeryHigh =>
        BiometricConfiguration.Default.GetDefaultProcessingOptions() with
        {
            Strategy = EncodingStrategy.TargetSize(
                BiometricConfiguration.Default.Standards.PivVeryHigh.TargetBytes
            ),
        };

    /// <summary>
    /// Archival: Fixed rate for long-term storage
    /// </summary>
    /// <value>
    /// Archival quality processing options for long-term storage and preservation.
    /// Uses configuration-based compression rate with metadata preservation and strict quality controls.
    /// </value>
    public static ProcessingOptions Archival =>
        BiometricConfiguration.Default.GetDefaultProcessingOptions() with
        {
            MinFaceConfidence = BiometricConfiguration.Default.FaceDetection.ArchivalMinConfidence,
            MaxRetries = BiometricConfiguration.Default.Performance.ExtendedRetries,
            ProcessingTimeout = BiometricConfiguration.Default.Performance.ExtendedTimeout,
            PreserveMetadata = true,
            Strategy = EncodingStrategy.FixedRate(
                BiometricConfiguration.Default.Encoding.Jpeg2000.ArchivalRate
            ),
        };

    /// <summary>
    /// Minimal file size: Target for smallest storage footprint
    /// </summary>
    /// <value>
    /// Minimal file size preset with target size from BiometricConfiguration.
    /// Uses aggressive compression with reduced quality.
    /// Suitable when storage space is the primary concern.
    /// </value>
    public static ProcessingOptions Minimal =>
        BiometricConfiguration.Default.GetDefaultProcessingOptions() with
        {
            Strategy = EncodingStrategy.TargetSize(
                BiometricConfiguration.Default.Standards.Minimal.TargetBytes
            ),
        };

    /// <summary>
    /// Fast processing: Fail-fast behavior with minimal retries
    /// </summary>
    /// <value>
    /// Fast processing preset with reduced retries and shorter timeout from BiometricConfiguration.
    /// Optimized for high-throughput batch processing where speed matters more than success rate.
    /// </value>
    public static ProcessingOptions Fast =>
        BiometricConfiguration.Default.GetDefaultProcessingOptions() with
        {
            MinFaceConfidence = BiometricConfiguration.Default.FaceDetection.RelaxedMinConfidence,
            MaxRetries = BiometricConfiguration.Default.Performance.FastRetries,
            ProcessingTimeout = BiometricConfiguration.Default.Performance.FastTimeout,
        };

    /// <summary>
    /// Creates ProcessingOptions from custom BiometricConfiguration
    /// </summary>
    public static ProcessingOptions FromConfiguration(BiometricConfiguration configuration)
    {
        return new ProcessingOptions
        {
            MinFaceConfidence = configuration.FaceDetection.MinConfidence,
            RequireSingleFace = configuration.FaceDetection.RequireSingleFace,
            MaxRetries = configuration.Performance.DefaultRetries,
            ProcessingTimeout = configuration.Performance.DefaultTimeout,
            PreserveMetadata = configuration.ImageProcessing.PreserveMetadata,
            RoiStartLevel = configuration.Encoding.Jpeg2000.DefaultRoiStartLevel,
            EnableRoi = configuration.Encoding.Jpeg2000.EnableRoiByDefault,
            AlignRoi = configuration.Encoding.Jpeg2000.AlignRoiByDefault,
            MaxRotationDegrees = configuration.FaceDetection.MaxRotationDegrees,
            Strategy = EncodingStrategy.TargetSize(configuration.Standards.PivBalanced.TargetBytes),
        };
    }
}
