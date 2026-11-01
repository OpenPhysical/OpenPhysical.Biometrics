// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CoreJ2K;
using CoreJ2K.j2k.util;
using CoreJ2K.Skia;
using CoreJ2K.Util;
using SkiaSharp;

/// <summary>Encodes prepared sRGB pixels for portrait previews and PIV records.</summary>
public static class PortraitEncoding
{
    private static readonly object EncoderLock = new();

    /// <summary>Encodes a complete image at a whole-image ratio no greater than 15:1.</summary>
    /// <param name="rgb">Tightly packed, opaque sRGB pixels.</param>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <returns>Owned JP2 bytes.</returns>
    /// <exception cref="ArgumentException">Pixel dimensions or buffer length are invalid.</exception>
    /// <exception cref="InvalidOperationException">The encoded image exceeds the compression limit.</exception>
    public static byte[] EncodeJpeg2000(ReadOnlySpan<byte> rgb, int width, int height)
    {
        using var image = Bitmap(rgb, width, height);
        lock (EncoderLock)
        {
            var previousLogger = FacilityManager.GetMsgLogger();
            FacilityManager.DefaultMsgLogger = new EncoderMessages();
            try
            {
                ImageFactory.Register(new SKBitmapImageCreator());
                // Smooth images can compress below a rate target. Try lossless encoding
                // before returning the compression-limit finding.
                foreach (var lossless in new[] { false, true })
                {
                    var parameters = J2kImage.GetDefaultEncoderParameterList();
                    parameters["rate"] = lossless ? "-1" : "1.6";
                    parameters["lossless"] = lossless ? "on" : "off";
                    parameters["file_format"] = "on";
                    parameters["Stiles"] = FormattableString.Invariant($"{width} {height}");
                    var bytes = J2kImage.ToBytes(image, parameters);
                    if ((long)bytes.Length * 15 >= rgb.Length)
                    {
                        var decoded = J2kImage.FromBytes(bytes);
                        if (
                            decoded.Width != width
                            || decoded.Height != height
                            || decoded.NumberOfComponents != 3
                        )
                            throw new InvalidOperationException(
                                "Encoded portrait dimensions changed."
                            );
                        return bytes;
                    }
                }
            }
            finally
            {
                FacilityManager.DefaultMsgLogger = previousLogger;
            }
        }
        throw new InvalidOperationException(
            "JPEG 2000 output exceeds the permitted compression ratio."
        );
    }

    /// <summary>Encodes a lossless PNG preview of the prepared pixels.</summary>
    /// <param name="rgb">Tightly packed, opaque sRGB pixels.</param>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <returns>Owned PNG bytes.</returns>
    public static byte[] EncodePng(ReadOnlySpan<byte> rgb, int width, int height)
    {
        using var bitmap = Bitmap(rgb, width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // The CLI owns its diagnostic output and reports stable status codes.
    private sealed class EncoderMessages : IMsgLogger
    {
        public void printmsg(int severity, string message) { }

        public void println(string message, int firstIndent, int indent) { }

        public void flush() { }
    }

    private static SKBitmap Bitmap(ReadOnlySpan<byte> rgb, int width, int height)
    {
        if (
            width is < 1 or > 4096
            || height is < 1 or > 4096
            || rgb.Length != (long)width * height * 3
        )
            throw new ArgumentException(
                "Supply valid dimensions and exactly three bytes per pixel.",
                nameof(rgb)
            );
        using var color = SKColorSpace.CreateSrgb();
        var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque, color)
        );
        var pixels = bitmap.GetPixelSpan();
        for (var i = 0; i < width * height; i++)
        {
            for (var c = 0; c < 3; c++)
                pixels[i * 4 + c] = rgb[i * 3 + c];
            pixels[i * 4 + 3] = 255;
        }
        return bitmap;
    }
}
