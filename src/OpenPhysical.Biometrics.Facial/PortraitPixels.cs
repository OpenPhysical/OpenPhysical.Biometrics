// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using SkiaSharp;

internal sealed record PortraitPixels(int Width, int Height, byte[] Rgb)
{
    internal static PortraitPixels Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length is < 1 or > 64 * 1024 * 1024)
            throw new ArgumentException(
                "Encoded portrait must contain at most 64 MiB.",
                nameof(encoded)
            );
        using var data = SKData.CreateCopy(encoded);
        using var codec =
            SKCodec.Create(data)
            ?? throw new ArgumentException("Unsupported or malformed image.", nameof(encoded));
        var w = codec.Info.Width;
        var h = codec.Info.Height;
        if (w < 1 || h < 1 || (long)w * h > 32 * 1024 * 1024)
            throw new ArgumentException(
                "Image dimensions exceed the portrait limit.",
                nameof(encoded)
            );
        using var color = SKColorSpace.CreateSrgb();
        using var bitmap = new SKBitmap(
            new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul, color)
        );
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new ArgumentException("Image decoding failed.", nameof(encoded));
        var origin = (int)codec.EncodedOrigin;
        var rotated = origin >= 5;
        var width = rotated ? h : w;
        var height = rotated ? w : h;
        var rgb = new byte[checked(width * height * 3)];
        var rgba = bitmap.GetPixelSpan();
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            // EXIF orientation is applied once, before any coordinates are exposed.
            var (dx, dy) = origin switch
            {
                2 => (w - 1 - x, y),
                3 => (w - 1 - x, h - 1 - y),
                4 => (x, h - 1 - y),
                5 => (y, x),
                6 => (h - 1 - y, x),
                7 => (h - 1 - y, w - 1 - x),
                8 => (y, w - 1 - x),
                _ => (x, y),
            };
            var i = (y * w + x) * 4;
            var o = (dy * width + dx) * 3;
            if (rgba[i + 3] != 255)
                throw new ArgumentException("Portrait pixels must be opaque.", nameof(encoded));
            for (var c = 0; c < 3; c++)
                rgb[o + c] = rgba[i + c];
        }
        return new(width, height, rgb);
    }

    internal byte[] Warp(int width, int height, PortraitTransform transform, bool quantize = false)
    {
        var output = new byte[checked(width * height * 3)];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var source = transform.Inverse(new(x, y));
            // OpenCV's reference model sampler quantizes bilinear coefficients to 1/32.
            var sx = quantize ? Math.Round(source.X * 32, MidpointRounding.ToEven) / 32 : source.X;
            var sy = quantize ? Math.Round(source.Y * 32, MidpointRounding.ToEven) / 32 : source.Y;
            var ix = (int)Math.Floor(sx);
            var iy = (int)Math.Floor(sy);
            var fx = sx - ix;
            var fy = sy - iy;
            for (var c = 0; c < 3; c++)
            {
                double sum = 0;
                for (var j = 0; j < 2; j++)
                for (var k = 0; k < 2; k++)
                    if (ix + k >= 0 && ix + k < Width && iy + j >= 0 && iy + j < Height)
                        sum +=
                            Rgb[((iy + j) * Width + ix + k) * 3 + c]
                            * (k == 0 ? 1 - fx : fx)
                            * (j == 0 ? 1 - fy : fy);
                output[(y * width + x) * 3 + c] = (byte)Math.Clamp(Math.Floor(sum + .5), 0, 255);
            }
        }
        return output;
    }
}
