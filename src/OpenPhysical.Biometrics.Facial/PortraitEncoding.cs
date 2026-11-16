// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Imaging;

/// <summary>Encodes prepared sRGB pixels and decodes retained portraits without running face models.</summary>
public static class PortraitEncoding
{
    /// <summary>Encodes a complete image at a whole-image ratio no greater than 15:1.</summary>
    /// <param name="rgb">Tightly packed, opaque sRGB pixels.</param>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <returns>Owned JP2 bytes, or a failure for invalid pixels or an encoding that exceeds the compression limit.</returns>
    public static Result<byte[], FacialError> EncodeJpeg2000(
        ReadOnlySpan<byte> rgb,
        int width,
        int height
    )
    {
        var valid = PortraitBitmap.Validate(rgb.Length, width, height);
        return valid.IsFailure
            ? valid.Error.Fail<byte[]>()
            : Jpeg2000Encoder.Encode(rgb.ToArray(), width, height);
    }

    /// <summary>Decodes a bounded JPEG 2000 portrait without detection, crop changes, or re-encoding.</summary>
    /// <param name="encoded">A JP2 file or raw codestream, limited to 64 MiB and 32 megapixels.</param>
    /// <returns>An opaque sRGB image owned by the caller, or a format, resource-limit, or decoding failure.</returns>
    public static Result<RgbImage, FacialError> DecodeJpeg2000(ReadOnlyMemory<byte> encoded) =>
        Jpeg2000Decoder.Decode(encoded);

    /// <summary>Encodes a lossless PNG preview of the prepared pixels.</summary>
    /// <param name="rgb">Tightly packed, opaque sRGB pixels.</param>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <returns>Owned PNG bytes, or a failure for invalid pixels.</returns>
    public static Result<byte[], FacialError> EncodePng(
        ReadOnlySpan<byte> rgb,
        int width,
        int height
    )
    {
        var valid = PortraitBitmap.Validate(rgb.Length, width, height);
        return valid.IsFailure ? valid.Error.Fail<byte[]>() : Png(rgb.ToArray(), width, height);
    }

    private static Result<byte[], FacialError> Png(byte[] rgb, int width, int height)
    {
        var created = RgbImageBytes.FromRgbBytes(rgb, width, height);
        Array.Clear(rgb);
        if (created.IsFailure)
            return new FacialError(FacialErrorCodes.ImageProcessingFailed, created.Error.Message);
        using var image = created.Value;
        using var output = new MemoryStream();
        try
        {
            return image
                .SaveAsPng(output)
                .MapError(error => new FacialError(
                    FacialErrorCodes.ImageProcessingFailed,
                    error.Message
                ))
                .Map(output.ToArray);
        }
        finally
        {
            Array.Clear(output.GetBuffer());
        }
    }
}
