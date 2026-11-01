// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

internal static class FacialErrors
{
    internal static FacialError Disposed() =>
        new(FacialErrorCodes.Disposed, "The portrait service was disposed.");

    internal static FacialError InvalidDimensions() =>
        new(FacialErrorCodes.InvalidDimensions, "Image dimensions must be from one through 4096.");

    internal static FacialError InvalidPixelBuffer() =>
        new(
            FacialErrorCodes.InvalidPixelBuffer,
            "Supply valid dimensions and exactly three bytes per pixel."
        );

    internal static FacialError EncodedImageSize() =>
        new(FacialErrorCodes.EncodedImageSize, "Encoded portrait must contain at most 64 MiB.");

    internal static FacialError UnsupportedImage() =>
        new(FacialErrorCodes.UnsupportedImage, "Unsupported or malformed image.");

    internal static FacialError ImageDimensionsExceeded() =>
        new(
            FacialErrorCodes.ImageDimensionsExceeded,
            "Image dimensions exceed the portrait limit."
        );

    internal static FacialError ImageDecodeFailed() =>
        new(FacialErrorCodes.ImageDecodeFailed, "Image decoding failed.");

    internal static FacialError ImageNotOpaque() =>
        new(FacialErrorCodes.ImageNotOpaque, "Portrait pixels must be opaque.");

    internal static FacialError GeometryMismatch() =>
        new(
            FacialErrorCodes.GeometryMismatch,
            "Reviewed geometry requires the matching source and face selection."
        );

    internal static FacialError GeometryPointInvalid() =>
        new(
            FacialErrorCodes.GeometryPointInvalid,
            "Reviewed points must lie within the oriented source."
        );

    internal static FacialError FaceIndexOutOfRange() =>
        new(FacialErrorCodes.FaceIndexOutOfRange, "The face index does not identify a detection.");

    internal static FacialError ModelMissing() =>
        new(FacialErrorCodes.ModelMissing, "The packaged MediaPipe model is missing.");

    internal static FacialError ModelChecksum() =>
        new(FacialErrorCodes.ModelChecksum, "The packaged MediaPipe model checksum is invalid.");

    internal static FacialError DetectorOutput() =>
        new(FacialErrorCodes.DetectorOutput, "Unexpected detector output dimensions.");

    internal static FacialError LandmarkOutput() =>
        new(FacialErrorCodes.LandmarkOutput, "Invalid landmark output.");

    internal static FacialError DetectionGeometry() =>
        new(FacialErrorCodes.DetectionGeometry, "Invalid detection geometry.");

    internal static FacialError InvalidLandmarks() =>
        new(
            FacialErrorCodes.InvalidLandmarks,
            "MediaPipe geometry requires exactly 478 landmarks."
        );

    internal static FacialError SingularTransform() =>
        new(FacialErrorCodes.SingularTransform, "Transform is singular.");

    internal static FacialError EncodedDimensions() =>
        new(FacialErrorCodes.EncodedDimensions, "Encoded portrait dimensions changed.");

    internal static FacialError CompressionLimit() =>
        new(
            FacialErrorCodes.CompressionLimit,
            "JPEG 2000 output exceeds the permitted compression ratio."
        );
}
