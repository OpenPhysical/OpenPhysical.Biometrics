// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using SkiaSharp;

internal static class GrayPngCodec
{
    internal static Result<byte[], DesktopError> Encode(
        ImmutableArray<byte> pixels,
        int width,
        int height
    ) =>
        DesktopEdge.Try(
            () => EncodeImage(pixels, width, height),
            "Storage.Image",
            "The source image could not be encoded."
        );

    internal static Result<ImmutableArray<byte>, DesktopError> Decode(
        byte[] bytes,
        int width,
        int height
    ) =>
        DesktopEdge
            .Try(
                () => DecodeImage(bytes, width, height),
                "Storage.Image",
                "The source image could not be decoded."
            )
            .Bind(result => result);

    private static byte[] EncodeImage(ImmutableArray<byte> pixels, int width, int height)
    {
        using var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque)
        );
        var data = pixels.ToArray();
        for (var row = 0; row < height; row++)
            Marshal.Copy(data, row * width, bitmap.GetPixels() + row * bitmap.RowBytes, width);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static Result<ImmutableArray<byte>, DesktopError> DecodeImage(
        byte[] bytes,
        int width,
        int height
    )
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (
            codec is null
            || codec.EncodedFormat != SKEncodedImageFormat.Png
            || codec.Info.Width != width
            || codec.Info.Height != height
        )
            return new DesktopError(
                "Storage.Image",
                "The source PNG geometry does not match its manifest."
            );
        using var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque)
        );
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            return new DesktopError(
                "Storage.Image",
                "The source PNG is incomplete or unsupported."
            );
        var pixels = new byte[width * height];
        for (var row = 0; row < height; row++)
            Marshal.Copy(bitmap.GetPixels() + row * bitmap.RowBytes, pixels, row * width, width);
        return ImmutableArray.Create(pixels);
    }
}
