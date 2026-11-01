// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

internal static class ImagingErrors
{
    internal static ImagingError Disposed() =>
        new(ImagingErrorCodes.Disposed, "The image was disposed.");

    internal static ImagingError InvalidDimensions() =>
        new(
            ImagingErrorCodes.InvalidDimensions,
            "Image dimensions must be positive and contain at most 64 megapixels."
        );

    internal static ImagingError PixelOutOfRange() =>
        new(ImagingErrorCodes.PixelOutOfRange, "The pixel coordinate is outside the image.");

    internal static ImagingError CropOutOfRange() =>
        new(
            ImagingErrorCodes.CropOutOfRange,
            "The crop rectangle must be non-empty and within the image."
        );

    internal static ImagingError FillOutOfRange() =>
        new(ImagingErrorCodes.FillOutOfRange, "The fill rectangle must be within the image.");

    internal static ImagingError RotationNotFinite() =>
        new(ImagingErrorCodes.RotationNotFinite, "The rotation angle must be finite.");

    internal static ImagingError OpacityOutOfRange() =>
        new(
            ImagingErrorCodes.OpacityOutOfRange,
            "Opacity must be finite and within zero through one."
        );

    internal static ImagingError PaddingTooSmall() =>
        new(
            ImagingErrorCodes.PaddingTooSmall,
            "Padding dimensions must contain the original image."
        );

    internal static ImagingError BlurSigmaOutOfRange() =>
        new(
            ImagingErrorCodes.BlurSigmaOutOfRange,
            "Blur sigma must be finite and within (0, 100]."
        );

    internal static ImagingError RgbLengthMismatch() =>
        new(
            ImagingErrorCodes.RgbLengthMismatch,
            "RGB data length differs from the size required by the supplied dimensions."
        );

    internal static ImagingError EncodedSizeInvalid() =>
        new(
            ImagingErrorCodes.EncodedSizeInvalid,
            "Encoded image size is outside the supported range."
        );

    internal static ImagingError FormatUnsupported() =>
        new(
            ImagingErrorCodes.FormatUnsupported,
            "The encoded image format is unsupported or malformed."
        );

    internal static ImagingError DecodeFailed() =>
        new(ImagingErrorCodes.DecodeFailed, "The encoded image is incomplete or malformed.");

    internal static ImagingError TiffMalformed(string detail) =>
        new(ImagingErrorCodes.TiffMalformed, detail);

    internal static ImagingError TiffUnsupported(string detail) =>
        new(ImagingErrorCodes.TiffUnsupported, detail);

    internal static ImagingError QualityOutOfRange() =>
        new(ImagingErrorCodes.QualityOutOfRange, "JPEG quality must be within one through 100.");

    internal static ImagingError EncodeFailed() =>
        new(ImagingErrorCodes.EncodeFailed, "Image encoding failed.");

    internal static ImagingError Cancelled() =>
        new(ImagingErrorCodes.Cancelled, "The operation was cancelled.");
}
