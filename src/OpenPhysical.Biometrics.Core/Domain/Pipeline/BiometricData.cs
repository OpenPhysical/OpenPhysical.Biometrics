using JetBrains.Annotations;
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Core.Domain.Pipeline;

/// <summary>
/// Immutable value object representing biometric data flowing through the pipeline.
/// </summary>
[PublicAPI]
public readonly record struct BiometricData(
    ImmutableArray<byte> RawData,
    BiometricDataType DataType,
    BiometricFormat Format,
    ImmutableDictionary<string, object> Metadata
)
{
    /// <summary>
    /// Creates biometric data from a byte array.
    /// </summary>
    public static BiometricData FromBytes(
        byte[] data, 
        BiometricDataType dataType, 
        BiometricFormat format
    ) => new(
        data.ToImmutableArray(), 
        dataType, 
        format, 
        ImmutableDictionary<string, object>.Empty
    );

    /// <summary>
    /// Creates biometric data from an immutable array.
    /// </summary>
    public static BiometricData FromBytes(
        ImmutableArray<byte> data, 
        BiometricDataType dataType, 
        BiometricFormat format
    ) => new(data, dataType, format, ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Returns new biometric data with additional metadata.
    /// </summary>
    public BiometricData WithMetadata(string key, object value) =>
        this with { Metadata = Metadata.SetItem(key, value) };

    /// <summary>
    /// Returns new biometric data with multiple metadata entries.
    /// </summary>
    public BiometricData WithMetadata(ImmutableDictionary<string, object> additionalMetadata) =>
        this with { Metadata = Metadata.SetItems(additionalMetadata) };

    /// <summary>
    /// Gets metadata value by key if it exists.
    /// </summary>
    public Maybe<T> GetMetadata<T>(string key)
    {
        if (!Metadata.TryGetValue(key, out var value))
            return Maybe<T>.None;

        return value is T typedValue ? Maybe<T>.From(typedValue) : Maybe<T>.None;
    }

    /// <summary>
    /// Gets the size of the raw data in bytes.
    /// </summary>
    public int SizeInBytes => RawData.Length;

    /// <summary>
    /// Converts the raw data back to a standard byte array.
    /// </summary>
    public byte[] ToByteArray() => RawData.ToArray();
}

/// <summary>
/// Enum representing the type of biometric data.
/// </summary>
[PublicAPI]
public enum BiometricDataType
{
    /// <summary>Facial image data</summary>
    FacialImage,
    /// <summary>Fingerprint image data</summary>
    FingerprintImage,
    /// <summary>Fingerprint minutiae data</summary>
    FingerprintMinutiae,
    /// <summary>Iris image data</summary>
    IrisImage,
    /// <summary>Multi-modal biometric data</summary>
    MultiModal,
    /// <summary>CBEFF container with biometric data</summary>
    CbeffContainer
}

/// <summary>
/// Immutable value object representing biometric data format.
/// </summary>
[PublicAPI]
public readonly record struct BiometricFormat(
    string FormatName,
    string MimeType,
    Maybe<string> StandardReference
)
{
    /// <summary>
    /// JPEG image format.
    /// </summary>
    public static readonly BiometricFormat Jpeg = new(
        "JPEG", 
        "image/jpeg", 
        Maybe<string>.None
    );

    /// <summary>
    /// JPEG 2000 image format.
    /// </summary>
    public static readonly BiometricFormat Jpeg2000 = new(
        "JPEG 2000", 
        "image/jp2", 
        Maybe<string>.From("ISO/IEC 15444-1")
    );

    /// <summary>
    /// PNG image format.
    /// </summary>
    public static readonly BiometricFormat Png = new(
        "PNG", 
        "image/png", 
        Maybe<string>.None
    );

    /// <summary>
    /// INCITS 385 facial image format.
    /// </summary>
    public static readonly BiometricFormat Incits385 = new(
        "INCITS 385", 
        "application/octet-stream", 
        Maybe<string>.From("ANSI/INCITS 385-2004")
    );

    /// <summary>
    /// INCITS 378 fingerprint minutiae format.
    /// </summary>
    public static readonly BiometricFormat Incits378 = new(
        "INCITS 378", 
        "application/octet-stream", 
        Maybe<string>.From("ANSI/INCITS 378-2004")
    );

    /// <summary>
    /// CBEFF format.
    /// </summary>
    public static readonly BiometricFormat Cbeff = new(
        "CBEFF", 
        "application/octet-stream", 
        Maybe<string>.From("ISO/IEC 19785")
    );
}

/// <summary>
/// Immutable value object representing processed biometric data with quality metrics.
/// </summary>
[PublicAPI]
public readonly record struct ProcessedBiometricData(
    BiometricData Data,
    QualityMetrics Quality,
    ImmutableList<DetectedFace> DetectedFaces,
    Maybe<FaceLandmarks> Landmarks,
    ImmutableDictionary<string, object> ProcessingMetadata
)
{
    /// <summary>
    /// Creates processed biometric data from basic biometric data.
    /// </summary>
    public static ProcessedBiometricData FromBiometricData(
        BiometricData data,
        QualityMetrics quality
    ) => new(
        data,
        quality,
        ImmutableList<DetectedFace>.Empty,
        Maybe<FaceLandmarks>.None,
        ImmutableDictionary<string, object>.Empty
    );

    /// <summary>
    /// Returns new processed data with detected faces.
    /// </summary>
    public ProcessedBiometricData WithDetectedFaces(ImmutableList<DetectedFace> faces) =>
        this with { DetectedFaces = faces };

    /// <summary>
    /// Returns new processed data with landmarks.
    /// </summary>
    public ProcessedBiometricData WithLandmarks(FaceLandmarks landmarks) =>
        this with { Landmarks = Maybe<FaceLandmarks>.From(landmarks) };

    /// <summary>
    /// Returns new processed data with processing metadata.
    /// </summary>
    public ProcessedBiometricData WithProcessingMetadata(string key, object value) =>
        this with { ProcessingMetadata = ProcessingMetadata.SetItem(key, value) };
}

/// <summary>
/// Immutable value object representing quality metrics for biometric data.
/// </summary>
[PublicAPI]
public readonly record struct QualityMetrics(
    QualityScore OverallQuality,
    Maybe<QualityScore> SharpnessScore,
    Maybe<QualityScore> ExposureScore,
    Maybe<QualityScore> GeometryScore,
    ImmutableDictionary<string, QualityScore> AdditionalMetrics
)
{
    /// <summary>
    /// Creates quality metrics with only overall quality.
    /// </summary>
    public static QualityMetrics WithOverallQuality(QualityScore overallQuality) => new(
        overallQuality,
        Maybe<QualityScore>.None,
        Maybe<QualityScore>.None,
        Maybe<QualityScore>.None,
        ImmutableDictionary<string, QualityScore>.Empty
    );

    /// <summary>
    /// Returns new quality metrics with sharpness score.
    /// </summary>
    public QualityMetrics WithSharpness(QualityScore sharpness) =>
        this with { SharpnessScore = Maybe<QualityScore>.From(sharpness) };

    /// <summary>
    /// Returns new quality metrics with exposure score.
    /// </summary>
    public QualityMetrics WithExposure(QualityScore exposure) =>
        this with { ExposureScore = Maybe<QualityScore>.From(exposure) };

    /// <summary>
    /// Returns new quality metrics with geometry score.
    /// </summary>
    public QualityMetrics WithGeometry(QualityScore geometry) =>
        this with { GeometryScore = Maybe<QualityScore>.From(geometry) };

    /// <summary>
    /// Returns new quality metrics with an additional metric.
    /// </summary>
    public QualityMetrics WithAdditionalMetric(string name, QualityScore score) =>
        this with { AdditionalMetrics = AdditionalMetrics.SetItem(name, score) };
}