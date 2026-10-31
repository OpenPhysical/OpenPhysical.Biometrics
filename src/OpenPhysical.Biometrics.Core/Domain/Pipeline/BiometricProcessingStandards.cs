using JetBrains.Annotations;

namespace OpenPhysical.Biometrics.Core.Domain.Pipeline;

/// <summary>
/// Immutable value object defining the standards and criteria for biometric processing.
/// Represents the acceptance criteria and constraints that determine how biometric data is processed.
/// </summary>
[PublicAPI]
public sealed record BiometricProcessingStandards(
    QualityStandard QualityStandard,
    float MinimumQualityThreshold,
    float MinimumFaceDetectionConfidence,
    int MaximumFacesPerImage
)
{
    /// <summary>
    /// Default PIV (Personal Identity Verification) processing standards.
    /// Based on NIST SP 800-76-2 requirements.
    /// </summary>
    public static BiometricProcessingStandards Piv { get; } =
        new(
            QualityStandard.NistSp800_76,
            MinimumQualityThreshold: 0.7f,
            MinimumFaceDetectionConfidence: 0.8f,
            MaximumFacesPerImage: 1
        );

    /// <summary>
    /// Default ICAO passport processing standards.
    /// Based on ICAO Doc 9303 requirements for machine-readable travel documents.
    /// </summary>
    public static BiometricProcessingStandards Icao { get; } =
        new(
            QualityStandard.IcaoDoc9303,
            MinimumQualityThreshold: 0.8f,
            MinimumFaceDetectionConfidence: 0.9f,
            MaximumFacesPerImage: 1
        );

    /// <summary>
    /// Default ISO/IEC 19794-5 processing standards.
    /// Based on international biometric data interchange format requirements.
    /// </summary>
    public static BiometricProcessingStandards Iso19794_5 { get; } =
        new(
            QualityStandard.Iso19794_5,
            MinimumQualityThreshold: 0.75f,
            MinimumFaceDetectionConfidence: 0.85f,
            MaximumFacesPerImage: 1
        );

    /// <summary>
    /// Lenient processing standards for testing or non-critical applications.
    /// </summary>
    public static BiometricProcessingStandards Lenient { get; } =
        new(
            QualityStandard.Custom,
            MinimumQualityThreshold: 0.5f,
            MinimumFaceDetectionConfidence: 0.6f,
            MaximumFacesPerImage: 5
        );

    /// <summary>
    /// Validates that all values are within acceptable ranges.
    /// </summary>
    public bool IsValid =>
        MinimumQualityThreshold is >= 0.0f and <= 1.0f
        && MinimumFaceDetectionConfidence is >= 0.0f and <= 1.0f
        && MaximumFacesPerImage > 0;

    /// <summary>
    /// Creates standards with adjusted quality threshold.
    /// </summary>
    public BiometricProcessingStandards WithQualityThreshold(float threshold) =>
        this with
        {
            MinimumQualityThreshold = threshold,
        };

    /// <summary>
    /// Creates standards with adjusted face detection confidence.
    /// </summary>
    public BiometricProcessingStandards WithFaceDetectionConfidence(float confidence) =>
        this with
        {
            MinimumFaceDetectionConfidence = confidence,
        };

    /// <summary>
    /// Creates standards with adjusted maximum faces per image.
    /// </summary>
    public BiometricProcessingStandards WithMaxFaces(int maxFaces) =>
        this with
        {
            MaximumFacesPerImage = maxFaces,
        };
}
