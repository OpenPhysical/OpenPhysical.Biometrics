using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Domains.Biometrics;
using QualityScore = OpenPhysical.Biometrics.Core.Domain.Quality.QualityScore;

namespace OpenPhysical.Biometrics.Core.Domain.Pipeline;

/// <summary>
/// Domain-specific pipeline step interfaces for biometric processing.
/// </summary>
[PublicAPI]
public static class BiometricPipelineSteps
{
    /// <summary>
    /// Step that detects faces in biometric image data.
    /// </summary>
    public interface IFaceDetectionStep : IPipelineStep<BiometricData, ProcessedBiometricData>
    {
        /// <summary>
        /// Gets the minimum confidence threshold for face detection.
        /// </summary>
        float MinConfidenceThreshold { get; }

        /// <summary>
        /// Gets whether multiple faces are allowed or if only single face detection is performed.
        /// </summary>
        bool AllowMultipleFaces { get; }
    }

    /// <summary>
    /// Step that assesses the quality of biometric data.
    /// </summary>
    public interface IQualityAssessmentStep
        : IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>
    {
        /// <summary>
        /// Gets the quality standards used for assessment.
        /// </summary>
        QualityStandard QualityStandard { get; }

        /// <summary>
        /// Gets the minimum acceptable quality threshold.
        /// </summary>
        QualityScore MinQualityThreshold { get; }
    }

    /// <summary>
    /// Step that extracts facial landmarks from detected faces.
    /// </summary>
    public interface ILandmarkExtractionStep
        : IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>
    {
        /// <summary>
        /// Gets the number of landmark points extracted.
        /// </summary>
        int LandmarkCount { get; }

        /// <summary>
        /// Gets the landmark model type used.
        /// </summary>
        LandmarkModelType ModelType { get; }
    }

    /// <summary>
    /// Step that transforms images (crop, rotate, resize).
    /// </summary>
    public interface IImageTransformStep
        : IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>
    {
        /// <summary>
        /// Gets the target image dimensions.
        /// </summary>
        ImageDimensions TargetDimensions { get; }

        /// <summary>
        /// Gets the maximum rotation angle allowed in degrees.
        /// </summary>
        float MaxRotationDegrees { get; }
    }

    /// <summary>
    /// Step that encodes images to JPEG 2000 with ROI support.
    /// </summary>
    public interface IJpeg2000EncodingStep : IPipelineStep<ProcessedBiometricData, BiometricData>
    {
        /// <summary>
        /// Gets the target compression ratio.
        /// </summary>
        float CompressionRatio { get; }

        /// <summary>
        /// Gets whether Region of Interest encoding is enabled.
        /// </summary>
        bool RoiEnabled { get; }

        /// <summary>
        /// Gets the target file size in bytes, if specified.
        /// </summary>
        Maybe<int> TargetSizeBytes { get; }
    }

    /// <summary>
    /// Step that packages biometric data into CBEFF containers.
    /// </summary>
    public interface ICbeffPackagingStep : IPipelineStep<BiometricData, BiometricData>
    {
        /// <summary>
        /// Gets the patron format for the CBEFF container.
        /// </summary>
        byte PatronFormat { get; }

        /// <summary>
        /// Gets whether to wrap with PIV BER tag.
        /// </summary>
        bool WrapWithPivTag { get; }
    }
}

/// <summary>
/// Enum representing quality standards for assessment.
/// </summary>
[PublicAPI]
public enum QualityStandard
{
    /// <summary>ISO/IEC 19794-5 facial image quality</summary>
    Iso19794_5,

    /// <summary>NIST SP 800-76 PIV biometric specifications</summary>
    NistSp800_76,

    /// <summary>ICAO Doc 9303 passport photo quality</summary>
    IcaoDoc9303,

    /// <summary>Custom quality standard</summary>
    Custom,
}

/// <summary>
/// Enum representing landmark model types.
/// </summary>
[PublicAPI]
public enum LandmarkModelType
{
    /// <summary>68-point iBUG facial landmark model</summary>
    iBug68Point,

    /// <summary>5-point facial landmark model (eyes, nose, mouth corners)</summary>
    FivePoint,

    /// <summary>98-point WFLW facial landmark model</summary>
    Wflw98Point,

    /// <summary>Custom landmark model</summary>
    Custom,
}
