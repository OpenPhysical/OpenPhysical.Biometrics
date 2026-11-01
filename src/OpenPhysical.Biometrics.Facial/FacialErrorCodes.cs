// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Defines the stable error codes returned by facial operations.</summary>
public static class FacialErrorCodes
{
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
