// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using SkiaSharp;

namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Decodes bounded encoded images into owned pixel buffers.</summary>
public static class RgbImageDecoder
{
    internal const int MaximumEncodedBytes = 256 * 1024 * 1024;
    private const int ChunkSize = 81920;

    /// <summary>Decodes one bounded encoded image from memory.</summary>
    /// <param name="data">Encoded image bytes.</param>
    /// <returns>An independent image owned by the caller, or the decoding failure.</returns>
    public static Result<RgbImage, ImagingError> Load(ReadOnlyMemory<byte> data) =>
        UnitResult
            .SuccessIf(
                data.Length is > 0 and <= MaximumEncodedBytes,
                ImagingErrors.EncodedSizeInvalid()
            )
            .Bind(() =>
                BaselineTiff.IsTiff(data.Span)
                    ? BaselineTiffDecoder.Decode(data)
                    : ImagingGuard.TryBind(() => DecodeCodec(data), ImagingErrorCodes.SkiaFailure)
            );

    /// <summary>Decodes an image file after checking its encoded size.</summary>
    /// <param name="path">Explicit input path.</param>
    /// <returns>An independent image owned by the caller, or the decoding or file failure.</returns>
    public static Result<RgbImage, ImagingError> Load(string path) =>
        ImagingGuard.TryBind(
            () =>
            {
                using var stream = File.OpenRead(path);
                return Load(stream);
            },
            ImagingErrorCodes.IoFailure
        );

    /// <summary>Decodes an image stream, reading at most the encoded-size limit.</summary>
    /// <param name="stream">Readable stream retained by its caller.</param>
    /// <returns>An independent image owned by the caller, or the decoding or stream failure.</returns>
    public static Result<RgbImage, ImagingError> Load(Stream stream) =>
        ImagingGuard
            .TryBind(() => ReadLimited(stream), ImagingErrorCodes.IoFailure)
            .Bind(DecodeAndWipe);

    /// <summary>Reads an image file with cancellation support before decoding its bounded contents.</summary>
    /// <param name="path">Explicit input path.</param>
    /// <param name="cancellationToken">Cancellation for file reads.</param>
    /// <returns>An independent image owned by the caller, or the decoding, file, or cancellation failure.</returns>
    public static async Task<Result<RgbImage, ImagingError>> LoadAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        var bytes = await ImagingGuard
            .TryAsync(() => ReadFileAsync(path, cancellationToken), ImagingErrorCodes.IoFailure)
            .ConfigureAwait(false);
        return bytes.Bind(DecodeAndWipe);
    }

    private static Result<RgbImage, ImagingError> DecodeAndWipe(byte[] bytes)
    {
        try
        {
            return Load(bytes);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private static Result<RgbImage, ImagingError> DecodeCodec(ReadOnlyMemory<byte> data)
    {
        using var encoded = SKData.CreateCopy(data.Span);
        var created = Maybe.From(SKCodec.Create(encoded));
        if (created.HasNoValue)
            return ImagingErrors.FormatUnsupported().Fail<RgbImage>();
        using var codec = created.Value;
        var info = codec.Info;
        if (!RgbImage.DimensionsValid(info.Width, info.Height))
            return ImagingErrors.InvalidDimensions().Fail<RgbImage>();
        using var bitmap = new SKBitmap(
            new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul)
        );
        return codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success
            ? Result.Success<RgbImage, ImagingError>(
                RgbImage.Adopt(SkiaBridge.ReadPixels(bitmap), info.Width, info.Height)
            )
            : ImagingErrors.DecodeFailed().Fail<RgbImage>();
    }

    private static Result<byte[], ImagingError> ReadLimited(Stream stream)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];
        try
        {
            int count;
            while ((count = stream.Read(chunk)) > 0)
            {
                if (buffer.Length + count > MaximumEncodedBytes)
                    return ImagingErrors.EncodedSizeInvalid().Fail<byte[]>();
                buffer.Write(chunk, 0, count);
            }
            return Result.Success<byte[], ImagingError>(buffer.ToArray());
        }
        finally
        {
            Array.Clear(chunk);
            Array.Clear(buffer.GetBuffer());
        }
    }

    private static async Task<Result<byte[], ImagingError>> ReadFileAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        if (new FileInfo(path).Length > MaximumEncodedBytes)
            return ImagingErrors.EncodedSizeInvalid().Fail<byte[]>();
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ChunkSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];
        try
        {
            int count;
            while (
                (count = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0
            )
            {
                if (buffer.Length + count > MaximumEncodedBytes)
                    return ImagingErrors.EncodedSizeInvalid().Fail<byte[]>();
                buffer.Write(chunk, 0, count);
            }
            return cancellationToken.IsCancellationRequested
                ? ImagingErrors.Cancelled().Fail<byte[]>()
                : Result.Success<byte[], ImagingError>(buffer.ToArray());
        }
        finally
        {
            Array.Clear(chunk);
            Array.Clear(buffer.GetBuffer());
        }
    }
}
