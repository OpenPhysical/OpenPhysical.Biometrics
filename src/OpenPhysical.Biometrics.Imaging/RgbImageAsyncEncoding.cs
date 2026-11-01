// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Cancellation-aware wrappers over <see cref="RgbImageEncoding"/>.</summary>
public static class RgbImageAsyncEncoding
{
    /// <summary>Writes PNG data and observes cancellation before encoding.</summary>
    /// <param name="image">Image to encode.</param>
    /// <param name="stream">Writable destination stream.</param>
    /// <param name="cancellationToken">Cancellation checked before encoding.</param>
    /// <returns>A completed task holding success, <see cref="ImagingErrorCodes.Cancelled"/>, or the encoding failure.</returns>
    public static Task<UnitResult<ImagingError>> SaveAsPngAsync(
        this RgbImage image,
        Stream stream,
        CancellationToken cancellationToken = default
    ) => Completed(cancellationToken, () => image.SaveAsPng(stream));

    /// <summary>Writes JPEG data and observes cancellation before encoding.</summary>
    /// <param name="image">Image to encode.</param>
    /// <param name="stream">Writable destination stream.</param>
    /// <param name="quality">JPEG quality from one through 100.</param>
    /// <param name="cancellationToken">Cancellation checked before encoding.</param>
    /// <returns>A completed task holding success, <see cref="ImagingErrorCodes.Cancelled"/>, or the encoding failure.</returns>
    public static Task<UnitResult<ImagingError>> SaveAsJpegAsync(
        this RgbImage image,
        Stream stream,
        int quality = 85,
        CancellationToken cancellationToken = default
    ) => Completed(cancellationToken, () => image.SaveAsJpeg(stream, quality));

    /// <summary>Writes baseline TIFF data and observes cancellation before encoding.</summary>
    /// <param name="image">Image to encode.</param>
    /// <param name="stream">Writable destination stream.</param>
    /// <param name="cancellationToken">Cancellation checked before encoding.</param>
    /// <returns>A completed task holding success, <see cref="ImagingErrorCodes.Cancelled"/>, or the encoding failure.</returns>
    public static Task<UnitResult<ImagingError>> SaveAsTiffAsync(
        this RgbImage image,
        Stream stream,
        CancellationToken cancellationToken = default
    ) => Completed(cancellationToken, () => image.SaveAsTiff(stream));

    /// <summary>Writes PNG data to an explicit file.</summary>
    /// <param name="image">Image to encode.</param>
    /// <param name="path">Destination path.</param>
    /// <param name="cancellationToken">Cancellation checked before opening the file.</param>
    /// <returns>A completed task holding success, <see cref="ImagingErrorCodes.Cancelled"/>, or the encoding failure.</returns>
    public static Task<UnitResult<ImagingError>> SaveAsPngAsync(
        this RgbImage image,
        string path,
        CancellationToken cancellationToken = default
    ) => Completed(cancellationToken, () => image.SaveAsPng(path));

    /// <summary>Writes JPEG data to an explicit file.</summary>
    /// <param name="image">Image to encode.</param>
    /// <param name="path">Destination path.</param>
    /// <param name="quality">JPEG quality from one through 100.</param>
    /// <param name="cancellationToken">Cancellation checked before opening the file.</param>
    /// <returns>A completed task holding success, <see cref="ImagingErrorCodes.Cancelled"/>, or the encoding failure.</returns>
    public static Task<UnitResult<ImagingError>> SaveAsJpegAsync(
        this RgbImage image,
        string path,
        int quality = 85,
        CancellationToken cancellationToken = default
    ) => Completed(cancellationToken, () => image.SaveAsJpeg(path, quality));

    private static Task<UnitResult<ImagingError>> Completed(
        CancellationToken cancellationToken,
        Func<UnitResult<ImagingError>> save
    ) =>
        Task.FromResult(
            cancellationToken.IsCancellationRequested
                ? UnitResult.Failure(ImagingErrors.Cancelled())
                : save()
        );
}
