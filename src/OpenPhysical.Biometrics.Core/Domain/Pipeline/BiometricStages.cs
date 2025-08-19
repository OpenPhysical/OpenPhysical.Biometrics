using OpenPhysical.Biometrics.Core.Domain.Common;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using JetBrains.Annotations;
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Core.Domain.Pipeline;

/// <summary>
/// Value object representing a raw biometric image with metadata
/// </summary>
[PublicAPI]
public sealed record BiometricImage(
    ImmutableArray<byte> Data,
    ImageMetadata Metadata);

/// <summary>
/// Value object representing the result of face detection stage
/// </summary>
[PublicAPI]
public sealed record DetectionStage(
    DetectedFace Face,
    BiometricImage CroppedRegion,
    BiometricImage OriginalImage);

/// <summary>
/// Value object representing the result of landmark alignment stage
/// </summary>
[PublicAPI]
public sealed record AlignmentStage(
    FaceLandmarks68 Landmarks,
    TransformMatrix Transform,
    DetectionStage Detection);

/// <summary>
/// Value object representing a standards-compliant image
/// </summary>
[PublicAPI]
public sealed record ComplianceStage(
    BiometricImage CompliantImage,
    ComplianceMetadata Metadata,
    DocumentStandard Standard);

/// <summary>
/// Value object representing encoded biometric data
/// </summary>
[PublicAPI]
public sealed record EncodedBiometricData(
    ImmutableArray<byte> Data,
    EncodingMetadata Metadata,
    DocumentStandard? Standard = null);

/// <summary>
/// Metadata about the image
/// </summary>
[PublicAPI]
public sealed record ImageMetadata(
    ImageDimensions Dimensions,
    string Format,
    int ColorDepth);

/// <summary>
/// Metadata about compliance with a standard
/// </summary>
[PublicAPI]
public sealed record ComplianceMetadata(
    bool IsCompliant,
    ImmutableList<string> Warnings,
    ImmutableDictionary<string, object> Properties);

/// <summary>
/// Metadata about encoding
/// </summary>
[PublicAPI]
public sealed record EncodingMetadata(
    string Format,
    int FileSize,
    float CompressionRatio,
    TimeSpan EncodingTime);

/// <summary>
/// Represents a transformation matrix for image operations
/// </summary>
[PublicAPI]
public sealed record TransformMatrix(
    float RotationDegrees,
    float ScaleX,
    float ScaleY,
    float TranslateX,
    float TranslateY);

/// <summary>
/// Represents a document standard (PIV, ICAO, etc.)
/// </summary>
[PublicAPI]
public sealed record DocumentStandard(
    string Name,
    string Version,
    ImageDimensions TargetDimensions);