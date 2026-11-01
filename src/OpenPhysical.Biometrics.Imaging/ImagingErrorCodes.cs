// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Stable error codes carried by <see cref="ImagingError"/>.</summary>
public static class ImagingErrorCodes
{
    /// <summary>The image was already disposed.</summary>
    public const string Disposed = "Imaging.Disposed";

    /// <summary>Dimensions are not positive or exceed 64 megapixels.</summary>
    public const string InvalidDimensions = "Imaging.InvalidDimensions";

    /// <summary>A pixel coordinate lies outside the image.</summary>
    public const string PixelOutOfRange = "Imaging.PixelOutOfRange";

    /// <summary>A crop rectangle is empty or exceeds the image bounds.</summary>
    public const string CropOutOfRange = "Imaging.CropOutOfRange";

    /// <summary>A fill rectangle exceeds the image bounds.</summary>
    public const string FillOutOfRange = "Imaging.FillOutOfRange";

    /// <summary>A rotation angle is not finite.</summary>
    public const string RotationNotFinite = "Imaging.RotationNotFinite";

    /// <summary>An opacity is not within zero through one.</summary>
    public const string OpacityOutOfRange = "Imaging.OpacityOutOfRange";

    /// <summary>Padding dimensions do not contain the original image.</summary>
    public const string PaddingTooSmall = "Imaging.PaddingTooSmall";

    /// <summary>A blur sigma is not within (0, 100].</summary>
    public const string BlurSigmaOutOfRange = "Imaging.BlurSigmaOutOfRange";

    /// <summary>Raw RGB data length does not match the dimensions.</summary>
    public const string RgbLengthMismatch = "Imaging.RgbLengthMismatch";

    /// <summary>Encoded input is empty or larger than the supported limit.</summary>
    public const string EncodedSizeInvalid = "Imaging.EncodedSizeInvalid";

    /// <summary>The encoded image format is unsupported or malformed.</summary>
    public const string FormatUnsupported = "Imaging.FormatUnsupported";

    /// <summary>The encoded image is incomplete or could not be decoded.</summary>
    public const string DecodeFailed = "Imaging.DecodeFailed";

    /// <summary>A TIFF stream is structurally invalid.</summary>
    public const string TiffMalformed = "Imaging.TiffMalformed";

    /// <summary>A TIFF stream uses features this decoder does not support.</summary>
    public const string TiffUnsupported = "Imaging.TiffUnsupported";

    /// <summary>A JPEG quality is not within one through 100.</summary>
    public const string QualityOutOfRange = "Imaging.QualityOutOfRange";

    /// <summary>The native encoder produced no data.</summary>
    public const string EncodeFailed = "Imaging.EncodeFailed";

    /// <summary>A file or stream operation failed.</summary>
    public const string IoFailure = "Imaging.IoFailure";

    /// <summary>A native Skia operation failed.</summary>
    public const string SkiaFailure = "Imaging.SkiaFailure";

    /// <summary>The caller cancelled the operation.</summary>
    public const string Cancelled = "Imaging.Cancelled";
}
