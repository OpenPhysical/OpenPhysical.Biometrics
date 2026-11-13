// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

using CSharpFunctionalExtensions;
using SkiaSharp;

// Keeps codec ownership and rasterization in one place; callers choose their image policy.
internal static class SkiaImageDecoder
{
    internal static Result<RgbImage, ImagingError> Decode(
        ReadOnlyMemory<byte> data,
        ImageDecodePolicy policy
    )
    {
        using var encoded = SKData.CreateCopy(data.Span);
        var created = Maybe.From(SKCodec.Create(encoded));
        if (created.HasNoValue)
            return ImagingErrors.FormatUnsupported().Fail<RgbImage>();
        using var codec = created.Value;
        var info = codec.Info;
        if (
            !RgbImage.DimensionsValid(info.Width, info.Height)
            || (long)info.Width * info.Height > policy.MaximumPixels
        )
            return ImagingErrors.InvalidDimensions().Fail<RgbImage>();
        return Rasterize(codec, policy);
    }

    private static Result<RgbImage, ImagingError> Rasterize(SKCodec codec, ImageDecodePolicy policy)
    {
        var info = codec.Info;
        using var srgb = SKColorSpace.CreateSrgb();
        using var bitmap = new SKBitmap(
            policy.Srgb
                ? new SKImageInfo(
                    info.Width,
                    info.Height,
                    SKColorType.Rgba8888,
                    SKAlphaType.Unpremul,
                    srgb
                )
                : new SKImageInfo(
                    info.Width,
                    info.Height,
                    SKColorType.Rgba8888,
                    SKAlphaType.Unpremul
                )
        );
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            return ImagingErrors.DecodeFailed().Fail<RgbImage>();
        return ImageOrientation.Apply(
            SkiaBridge.ReadPixels(bitmap),
            info.Width,
            info.Height,
            policy.Orientation ? (int)codec.EncodedOrigin : 1,
            policy.Opaque
        );
    }
}
