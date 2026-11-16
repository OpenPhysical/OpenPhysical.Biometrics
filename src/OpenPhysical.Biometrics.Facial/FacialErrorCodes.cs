// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Defines the stable error codes returned by facial operations.</summary>
public static class FacialErrorCodes
{
    /// <summary>The background-editing parameters are outside their supported bounds.</summary>
    public const string InvalidBackgroundPolicy = "InvalidBackgroundPolicy";

    /// <summary>The person mask does not belong to the exact source-supported portrait.</summary>
    public const string BackgroundMaskMismatch = "BackgroundMaskMismatch";

    /// <summary>The person segmentation model returned invalid dimensions or probabilities.</summary>
    public const string SegmentationOutput = "SegmentationOutput";

    /// <summary>A confidence sample is outside the mask's source-image coordinates.</summary>
    public const string MaskSampleOutsideSource = "MaskSampleOutsideSource";

    /// <summary>Capture override evidence is ineligible, stale or bound to another source.</summary>
    public const string InvalidCaptureOverride = "InvalidCaptureOverride";

    /// <summary>Source image checks still require correction, recapture or an explicit allowed override.</summary>
    public const string CaptureReviewRequired = "CaptureReviewRequired";

    /// <summary>Crown and ear measurements still need review.</summary>
    public const string GeometryReviewRequired = "GeometryReviewRequired";

    /// <summary>The mouth assessment policy is outside its supported range.</summary>
    public const string InvalidMouthPolicy = "InvalidMouthPolicy";

    /// <summary>Landmark geometry or numerical estimation cannot support a source pose measurement.</summary>
    public const string PoseUnassessable = "PoseUnassessable";

    /// <summary>The service has been disposed.</summary>
    public const string Disposed = "Disposed";

    /// <summary>Image dimensions are outside the supported range.</summary>
    public const string InvalidDimensions = "InvalidDimensions";

    /// <summary>The pixel buffer does not contain exactly three bytes per pixel.</summary>
    public const string InvalidPixelBuffer = "InvalidPixelBuffer";

    /// <summary>The encoded image is empty or exceeds 64 MiB.</summary>
    public const string EncodedImageSize = "EncodedImageSize";

    /// <summary>The encoded image format is unsupported or malformed.</summary>
    public const string UnsupportedImage = "UnsupportedImage";

    /// <summary>The decoded image exceeds the portrait pixel limit.</summary>
    public const string ImageDimensionsExceeded = "ImageDimensionsExceeded";

    /// <summary>The image codec failed to decode the pixels.</summary>
    public const string ImageDecodeFailed = "ImageDecodeFailed";

    /// <summary>The portrait contains transparent pixels.</summary>
    public const string ImageNotOpaque = "ImageNotOpaque";

    /// <summary>Reviewed geometry does not match the source or face selection.</summary>
    public const string GeometryMismatch = "GeometryMismatch";

    /// <summary>A reviewed point is not finite or lies outside the oriented source.</summary>
    public const string GeometryPointInvalid = "GeometryPointInvalid";

    /// <summary>The selected face index does not identify a detection.</summary>
    public const string FaceIndexOutOfRange = "FaceIndexOutOfRange";

    /// <summary>A packaged model resource is missing.</summary>
    public const string ModelMissing = "ModelMissing";

    /// <summary>A packaged model does not match its pinned checksum.</summary>
    public const string ModelChecksum = "ModelChecksum";

    /// <summary>A packaged model could not be read or loaded.</summary>
    public const string ModelLoadFailed = "ModelLoadFailed";

    /// <summary>The model runtime failed while running inference.</summary>
    public const string ModelExecutionFailed = "ModelExecutionFailed";

    /// <summary>A required native runtime library is unavailable.</summary>
    public const string NativeRuntimeUnavailable = "NativeRuntimeUnavailable";

    /// <summary>The detector returned unexpected output dimensions.</summary>
    public const string DetectorOutput = "DetectorOutput";

    /// <summary>The landmark network returned invalid output.</summary>
    public const string LandmarkOutput = "LandmarkOutput";

    /// <summary>Detections contain invalid geometry.</summary>
    public const string DetectionGeometry = "DetectionGeometry";

    /// <summary>The landmark set does not contain exactly 478 points.</summary>
    public const string InvalidLandmarks = "InvalidLandmarks";

    /// <summary>The transform cannot be inverted.</summary>
    public const string SingularTransform = "SingularTransform";

    /// <summary>An image library failed while processing pixels.</summary>
    public const string ImageProcessingFailed = "ImageProcessingFailed";

    /// <summary>The JPEG 2000 encoder failed.</summary>
    public const string EncodingFailed = "EncodingFailed";

    /// <summary>The encoded portrait decoded to different dimensions.</summary>
    public const string EncodedDimensions = "EncodedDimensions";

    /// <summary>The JPEG 2000 output exceeds the permitted compression ratio.</summary>
    public const string CompressionLimit = "CompressionLimit";
}
