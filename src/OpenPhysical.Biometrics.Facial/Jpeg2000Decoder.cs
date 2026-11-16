// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CoreJ2K;
using CoreJ2K.Configuration;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Imaging;
using SkiaSharp;

internal static class Jpeg2000Decoder
{
    internal static Result<RgbImage, FacialError> Decode(ReadOnlyMemory<byte> encoded)
    {
        var valid = Jpeg2000Input.Check(encoded.Span);
        if (valid.IsFailure)
            return valid.Error;
        return CoreJpeg2000.Run(
            () => DecodeBounded(encoded),
            FacialErrorCodes.ImageDecodeFailed,
            "The JPEG 2000 portrait could not be decoded."
        );
    }

    private static Result<RgbImage, FacialError> DecodeBounded(ReadOnlyMemory<byte> encoded)
    {
        // The codec checks SIZ tile tables and output geometry before image-sized allocations.
        var parameters = new J2KDecoderConfiguration()
            .WithHighestResolution()
            .WithLimits(
                DecoderLimits
                    .Strict.WithMaxPixels(PortraitImage.MaximumPixels)
                    .WithMaxTileComponents(65536)
            )
            .ToParameterList();
        var bytes = encoded.ToArray();
        try
        {
            using var bitmap = J2kImage.DecodeToImage<SKBitmap>(bytes, parameters);
            try
            {
                return Copy(bitmap);
            }
            finally
            {
                bitmap.GetPixelSpan().Clear();
            }
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private static Result<RgbImage, FacialError> Copy(SKBitmap bitmap)
    {
        var created = RgbImage
            .Create(bitmap.Width, bitmap.Height)
            .MapError(error => new FacialError(FacialErrorCodes.ImageDecodeFailed, error.Message));
        if (created.IsFailure)
            return created.Error;
        var image = created.Value;
        for (var y = 0; y < bitmap.Height; y++)
        {
            _ = image.TryGetRowSpan(y, out var row);
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha != 255)
                {
                    image.Dispose();
                    return FacialErrors.ImageNotOpaque().Fail<RgbImage>();
                }
                row[x] = new RgbaPixel(pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha);
            }
        }
        return image;
    }
}
