using System.Collections.Immutable;
using System.Drawing;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// Pure encoding service that encodes images to various formats without any transformations.
/// This service focuses solely on format conversion and compression.
/// </summary>
[PublicAPI]
public interface IBiometricEncodingService
{
    /// <summary>
    /// Encodes an image to JPEG 2000 format with optional ROI support
    /// </summary>
    Result<EncodedImageData> EncodeToJpeg2000(RgbImage image, Jpeg2000EncodingOptions options);

    /// <summary>
    /// Encodes an image to JPEG format
    /// </summary>
    Result<EncodedImageData> EncodeToJpeg(RgbImage image, int quality = 85);

    /// <summary>
    /// Encodes an image to PNG format
    /// </summary>
    Result<EncodedImageData> EncodeToPng(RgbImage image);

    /// <summary>
    /// Encodes an image to TIFF format
    /// </summary>
    Result<EncodedImageData> EncodeToTiff(RgbImage image);
}

/// <summary>
/// Result of image encoding
/// </summary>
[PublicAPI]
public sealed record EncodedImageData(
    ImmutableArray<byte> Data,
    string Format,
    int FileSize,
    ImageDimensions Dimensions
);

/// <summary>
/// Options for JPEG 2000 encoding
/// </summary>
[PublicAPI]
public sealed record Jpeg2000EncodingOptions(
    EncodingStrategy Strategy,
    FacialRoiSet? RoiSet = null,
    int RoiStartLevel = 3,
    bool EnableRoi = true,
    bool AlignRoi = false
);

/// <summary>
/// Implementation of pure encoding service
/// </summary>
[PublicAPI]
public sealed class BiometricEncodingService : IBiometricEncodingService
{
    private readonly IJpeg2000Encoder _jpeg2000Encoder;
    private readonly ILogger<BiometricEncodingService> _logger;

    /// <summary>Creates the service with its processing dependencies and operational diagnostics.</summary>
    /// <param name="jpeg2000Encoder">JPEG 2000 encoder used for the selected compression profile.</param>
    /// <param name="logger">Operational diagnostics for this service.</param>
    public BiometricEncodingService(
        IJpeg2000Encoder jpeg2000Encoder,
        ILogger<BiometricEncodingService> logger
    )
    {
        _jpeg2000Encoder = jpeg2000Encoder;
        _logger = logger;
    }

    /// <summary>Encodes caller-owned facial pixels with the selected JPEG 2000 settings.</summary>
    /// <param name="image">Caller-owned source pixels, borrowed for the encoding call.</param>
    /// <param name="options">Processing and compression settings, or null to use the profile defaults.</param>
    /// <returns>The encoded image and its output metadata, or a processing failure.</returns>
    public Result<EncodedImageData> EncodeToJpeg2000(
        RgbImage image,
        Jpeg2000EncodingOptions options
    )
    {
        _logger.LogDebug(
            new EventId(633700, "BiometricEncodingService"),
            "BiometricEncodingService processing status changed."
        );

        var roiSet = options.RoiSet ?? CreateDefaultRoiSet(image);

        var baseRate = options.Strategy switch
        {
            FixedRateStrategy fixedRate => fixedRate.Rate,
            TargetSizeStrategy targetSize => CalculateRateForTargetSize(
                targetSize.TargetBytes,
                image.Width,
                image.Height
            ),
            _ => 1.0f,
        };

        var encodeResult = _jpeg2000Encoder.EncodeWithRoi(
            image,
            roiSet,
            baseRate,
            options.RoiStartLevel,
            options.EnableRoi,
            options.AlignRoi
        );

        if (encodeResult.IsFailure)
            return Result.Failure<EncodedImageData>(
                $"JPEG 2000 encoding failed: {encodeResult.Error}"
            );

        var data = encodeResult.Value.ToImmutableArray();
        _logger.LogInformation(
            new EventId(633701, "BiometricEncodingService"),
            "BiometricEncodingService processing completed."
        );

        return Result.Success(
            new EncodedImageData(
                data,
                "JP2",
                data.Length,
                new ImageDimensions(image.Width, image.Height)
            )
        );
    }

    /// <summary>Encodes caller-owned facial pixels as JPEG.</summary>
    /// <param name="image">Caller-owned source pixels, borrowed for the encoding call.</param>
    /// <param name="quality">JPEG encoder quality from one through 100.</param>
    /// <returns>The encoded image and its output metadata, or a processing failure.</returns>
    public Result<EncodedImageData> EncodeToJpeg(RgbImage image, int quality = 85)
    {
        if (quality < 1 || quality > 100)
            return Result.Failure<EncodedImageData>("JPEG quality must be between 1 and 100");

        return Result.Try(() =>
        {
            _logger.LogDebug(
                new EventId(633702, "BiometricEncodingService"),
                "BiometricEncodingService processing status changed."
            );

            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms, quality);

            var data = ms.ToArray().ToImmutableArray();
            _logger.LogInformation(
                new EventId(633703, "BiometricEncodingService"),
                "BiometricEncodingService processing completed."
            );

            return new EncodedImageData(
                data,
                "JPEG",
                data.Length,
                new ImageDimensions(image.Width, image.Height)
            );
        });
    }

    /// <summary>Encodes caller-owned facial pixels as lossless PNG.</summary>
    /// <param name="image">Caller-owned source pixels, borrowed for the encoding call.</param>
    /// <returns>The encoded image and its output metadata, or a processing failure.</returns>
    public Result<EncodedImageData> EncodeToPng(RgbImage image)
    {
        return Result.Try(() =>
        {
            _logger.LogDebug(
                new EventId(633704, "BiometricEncodingService"),
                "BiometricEncodingService processing status changed."
            );

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);

            var data = ms.ToArray().ToImmutableArray();
            _logger.LogInformation(
                new EventId(633705, "BiometricEncodingService"),
                "BiometricEncodingService processing completed."
            );

            return new EncodedImageData(
                data,
                "PNG",
                data.Length,
                new ImageDimensions(image.Width, image.Height)
            );
        });
    }

    /// <summary>Encodes caller-owned facial pixels as baseline RGB TIFF.</summary>
    /// <param name="image">Caller-owned source pixels, borrowed for the encoding call.</param>
    /// <returns>The encoded image and its output metadata, or a processing failure.</returns>
    public Result<EncodedImageData> EncodeToTiff(RgbImage image)
    {
        return Result.Try(() =>
        {
            _logger.LogDebug(
                new EventId(633706, "BiometricEncodingService"),
                "BiometricEncodingService processing status changed."
            );

            using var ms = new MemoryStream();
            image.SaveAsTiff(ms);

            var data = ms.ToArray().ToImmutableArray();
            _logger.LogInformation(
                new EventId(633707, "BiometricEncodingService"),
                "BiometricEncodingService processing completed."
            );

            return new EncodedImageData(
                data,
                "TIFF",
                data.Length,
                new ImageDimensions(image.Width, image.Height)
            );
        });
    }

    private static FacialRoiSet CreateDefaultRoiSet(RgbImage image)
    {
        // Create a minimal ROI set for the entire image when no ROI is specified
        var roiBounds = new RoiBoundingBox(0, 0, image.Width, image.Height);
        var roiRegion = new RoiRegion("FullImage", 1, roiBounds, new List<int> { 0, 1, 2, 3, 4 });
        return new FacialRoiSet(roiRegion);
    }

    private static float CalculateRateForTargetSize(int targetBytes, int width, int height)
    {
        // Estimate compression rate needed for target size
        // This is a rough approximation: rate = (targetBytes * 8) / (width * height)
        var totalPixels = width * height;
        var targetBits = targetBytes * 8;
        return Math.Max(0.1f, Math.Min(5.0f, (float)targetBits / totalPixels));
    }
}
