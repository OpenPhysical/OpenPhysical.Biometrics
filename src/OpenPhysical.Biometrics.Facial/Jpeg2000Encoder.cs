// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CoreJ2K;
using CSharpFunctionalExtensions;
using SkiaSharp;

// Encodes the existing PIV portrait policy without changing codec parameters.
internal static class Jpeg2000Encoder
{
    internal static Result<byte[], FacialError> Encode(byte[] rgb, int width, int height)
    {
        try
        {
            return CoreJpeg2000.Run(
                () =>
                {
                    using var image = PortraitBitmap.Create(rgb, width, height);
                    return Attempts(image, rgb.Length, width, height);
                },
                FacialErrorCodes.EncodingFailed,
                "The JPEG 2000 encoder failed."
            );
        }
        finally
        {
            Array.Clear(rgb);
        }
    }

    // Smooth images can compress below a rate target. Try lossless encoding
    // before returning the compression-limit finding.
    private static Result<byte[], FacialError> Attempts(
        SKBitmap image,
        int rawLength,
        int width,
        int height
    )
    {
        foreach (var lossless in new[] { false, true })
        {
            var attempt = Attempt(image, rawLength, width, height, lossless);
            if (attempt.IsFailure || attempt.Value.HasValue)
                return attempt.IsFailure
                    ? attempt.Error.Fail<byte[]>()
                    : attempt.Value.Value.Succeed();
        }
        return FacialErrors.CompressionLimit().Fail<byte[]>();
    }

    private static Result<Maybe<byte[]>, FacialError> Attempt(
        SKBitmap image,
        int rawLength,
        int width,
        int height,
        bool lossless
    )
    {
        var parameters = J2kImage.GetDefaultEncoderParameterList();
        parameters["rate"] = lossless ? "-1" : "1.6";
        parameters["lossless"] = lossless ? "on" : "off";
        parameters["file_format"] = "on";
        parameters["Stiles"] = FormattableString.Invariant($"{width} {height}");
        var bytes = J2kImage.ToBytes(image, parameters);
        if ((long)bytes.Length * 15 < rawLength)
            return Maybe<byte[]>.None.Succeed();
        using var decoded = J2kImage.FromBytes(bytes);
        return decoded.Width != width || decoded.Height != height || decoded.NumberOfComponents != 3
            ? FacialErrors.EncodedDimensions().Fail<Maybe<byte[]>>()
            : Maybe.From(bytes).Succeed();
    }
}
