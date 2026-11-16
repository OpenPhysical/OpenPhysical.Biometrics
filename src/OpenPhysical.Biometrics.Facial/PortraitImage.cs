// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Buffers.Binary;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto.Digests;

/// <summary>An owned, orientation-corrected, opaque sRGB image for portrait preparation.</summary>
public sealed class PortraitImage
{
    internal const long MaximumPixels = 32 * 1024 * 1024;
    internal const int MaximumEncodedBytes = 64 * 1024 * 1024;

    private PortraitImage(PortraitPixels pixels, string sourceSha256)
    {
        Pixels = pixels;
        SourceSha256 = sourceSha256;
    }

    /// <summary>Gets the oriented source width in pixels.</summary>
    public int Width => Pixels.Width;

    /// <summary>Gets the oriented source height in pixels.</summary>
    public int Height => Pixels.Height;

    /// <summary>Gets owned, tightly packed, top-left-origin sRGB samples.</summary>
    public ImmutableArray<byte> RgbPixels => Pixels.Rgb;

    /// <summary>Gets the encoded source hash, or a domain-separated geometry-and-pixel hash for camera RGB input.</summary>
    public string SourceSha256 { get; }

    internal PortraitPixels Pixels { get; }

    /// <summary>Decodes an image into sRGB and applies its EXIF orientation once.</summary>
    /// <param name="encoded">Original encoded bytes, limited to 64 MiB.</param>
    /// <returns>An immutable image, or a precise size, decoding, or transparency failure.</returns>
    public static Result<PortraitImage, FacialError> Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length is < 1 or > MaximumEncodedBytes)
            return FacialErrors.EncodedImageSize().Fail<PortraitImage>();
        var source = encoded.ToArray();
        return PortraitDecoder
            .Decode(source)
            .Map(pixels => new PortraitImage(pixels, Sha256.HexDigest(source)));
    }

    /// <summary>Owns camera samples already converted to opaque eight-bit sRGB without resizing them.</summary>
    /// <param name="rgb">Exactly three samples per pixel, in red, green, blue order.</param>
    /// <param name="width">Positive source width.</param>
    /// <param name="height">Positive source height; total size is limited to 32 megapixels.</param>
    /// <returns>An immutable image, or an invalid pixel-buffer failure.</returns>
    public static Result<PortraitImage, FacialError> FromSrgb(
        ReadOnlySpan<byte> rgb,
        int width,
        int height
    )
    {
        if (
            width < 1
            || height < 1
            || (long)width * height > MaximumPixels
            || rgb.Length != (long)width * height * 3
        )
            return FacialErrors.InvalidPixelBuffer().Fail<PortraitImage>();
        var pixels = new PortraitPixels(width, height, ImmutableArray.Create(rgb));
        return new PortraitImage(pixels, RawIdentity(pixels)).Succeed();
    }

    private static string RawIdentity(PortraitPixels pixels)
    {
        var digest = new Sha256Digest();
        digest.BlockUpdate("OpenPhysical.Portrait.sRGB24/v1"u8);
        Span<byte> dimensions = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(dimensions, pixels.Width);
        BinaryPrimitives.WriteInt32BigEndian(dimensions[4..], pixels.Height);
        digest.BlockUpdate(dimensions);
        digest.BlockUpdate(pixels.Rgb.AsSpan());
        Span<byte> hash = stackalloc byte[32];
        _ = digest.DoFinal(hash);
        return Convert.ToHexStringLower(hash);
    }
}
