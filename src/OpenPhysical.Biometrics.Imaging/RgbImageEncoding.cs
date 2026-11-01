// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using SkiaSharp;

namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Writes images to caller-owned streams or explicit files.</summary>
public static class RgbImageEncoding
{
    /// <summary>Writes lossless PNG data to a caller-owned stream.</summary>
    /// <param name="image">Image to encode.</param>
    /// <param name="stream">Writable destination stream.</param>
    /// <returns>Success, or the encoding failure.</returns>
    public static UnitResult<ImagingError> SaveAsPng(this RgbImage image, Stream stream) =>
        Encode(image, stream, SKEncodedImageFormat.Png, 100);

    /// <summary>Writes JPEG data to a caller-owned stream.</summary>
    /// <param name="image">Image to encode.</param>
    /// <param name="stream">Writable destination stream.</param>
    /// <param name="quality">JPEG quality from one through 100.</param>
    /// <returns>Success, or the encoding failure.</returns>
    public static UnitResult<ImagingError> SaveAsJpeg(
        this RgbImage image,
        Stream stream,
        int quality = 85
    ) => Encode(image, stream, SKEncodedImageFormat.Jpeg, quality);

    /// <summary>Writes an uncompressed RGB baseline TIFF with a 72 ppi display resolution.</summary>
    /// <remarks>The resolution tags describe the default display size of this pixel buffer.</remarks>
    /// <param name="image">Image to encode.</param>
    /// <param name="stream">Writable destination stream.</param>
    /// <returns>Success, or the encoding failure.</returns>
    public static UnitResult<ImagingError> SaveAsTiff(this RgbImage image, Stream stream) =>
        BaselineTiffEncoder.Encode(image, stream);

    /// <summary>Writes lossless PNG data to an explicit file.</summary>
    /// <param name="image">Image to encode.</param>
    /// <param name="path">Destination path.</param>
    /// <returns>Success, or the encoding or file failure.</returns>
    public static UnitResult<ImagingError> SaveAsPng(this RgbImage image, string path) =>
        ToFile(path, stream => image.SaveAsPng(stream));

    /// <summary>Writes JPEG data to an explicit file.</summary>
    /// <param name="image">Image to encode.</param>
    /// <param name="path">Destination path.</param>
    /// <param name="quality">JPEG quality from one through 100.</param>
    /// <returns>Success, or the encoding or file failure.</returns>
    public static UnitResult<ImagingError> SaveAsJpeg(
        this RgbImage image,
        string path,
        int quality = 85
    ) => ToFile(path, stream => image.SaveAsJpeg(stream, quality));

    private static UnitResult<ImagingError> ToFile(
        string path,
        Func<Stream, UnitResult<ImagingError>> write
    ) =>
        ImagingGuard.TryUnit(
            () =>
            {
                using var stream = File.Create(path);
                return write(stream);
            },
            ImagingErrorCodes.IoFailure
        );

    private static UnitResult<ImagingError> Encode(
        RgbImage image,
        Stream stream,
        SKEncodedImageFormat format,
        int quality
    ) =>
        image
            .Active()
            .Ensure(_ => quality is >= 1 and <= 100, ImagingErrors.QualityOutOfRange())
            .Bind(active =>
                SkiaBridge.WithBitmap(
                    active,
                    bitmap => EncodeBitmap(bitmap, stream, format, quality)
                )
            )
            .AsUnit();

    private static Result<int, ImagingError> EncodeBitmap(
        SKBitmap bitmap,
        Stream stream,
        SKEncodedImageFormat format,
        int quality
    ) =>
        ImagingGuard.TryBind(
            () =>
            {
                using var image = SKImage.FromBitmap(bitmap);
                var encoded = Maybe.From(image.Encode(format, quality));
                if (encoded.HasNoValue)
                    return ImagingErrors.EncodeFailed().Fail<int>();
                using var data = encoded.Value;
                data.SaveTo(stream);
                return Result.Success<int, ImagingError>((int)data.Size);
            },
            ImagingErrorCodes.IoFailure
        );
}
