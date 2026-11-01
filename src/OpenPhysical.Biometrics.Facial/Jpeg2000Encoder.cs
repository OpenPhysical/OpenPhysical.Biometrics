// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CoreJ2K;
using CoreJ2K.j2k.util;
using CoreJ2K.Skia;
using CoreJ2K.Util;
using CSharpFunctionalExtensions;
using SkiaSharp;

// Runs the shared CoreJ2K encoder, which keeps process-wide logger and factory state.
internal static class Jpeg2000Encoder
{
    private static readonly object EncoderLock = new();

    internal static Result<byte[], FacialError> Encode(byte[] rgb, int width, int height) =>
        FacialEdge.TryBind(
            () => EncodeLocked(rgb, width, height),
            FacialErrorCodes.EncodingFailed,
            "The JPEG 2000 encoder failed."
        );

    private static Result<byte[], FacialError> EncodeLocked(byte[] rgb, int width, int height)
    {
        using var image = PortraitBitmap.Create(rgb, width, height);
        lock (EncoderLock)
        {
            var previousLogger = FacilityManager.GetMsgLogger();
            FacilityManager.DefaultMsgLogger = new Jpeg2000MessageSink();
            try
            {
                ImageFactory.Register(new SKBitmapImageCreator());
                return Attempts(image, rgb.Length, width, height);
            }
            finally
            {
                FacilityManager.DefaultMsgLogger = previousLogger;
            }
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
        var decoded = J2kImage.FromBytes(bytes);
        return decoded.Width != width || decoded.Height != height || decoded.NumberOfComponents != 3
            ? FacialErrors.EncodedDimensions().Fail<Maybe<byte[]>>()
            : Maybe.From(bytes).Succeed();
    }
}
