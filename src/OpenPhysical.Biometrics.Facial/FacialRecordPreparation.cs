// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using System.Globalization;
using CoreJ2K;
using CoreJ2K.Skia;
using CoreJ2K.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Piv;
using SkiaSharp;

/// <summary>Records the camera and operator findings attached to the captured sRGB pixels.</summary>
/// <param name="CapturedAt">The acquisition instant.</param>
/// <param name="DeviceType">The camera's device identifier.</param>
/// <param name="SourceType">Two for a digital still camera or six for digital video.</param>
/// <param name="FullFrontalConfirmed">Whether acquisition confirmed a single neutral, full-frontal subject.</param>
/// <param name="OpticalResolutionConfirmed">Whether acquisition confirmed the required optical resolution and geometry.</param>
/// <param name="Quality">Recorded facial quality from zero through 100; zero denotes unspecified quality.</param>
public sealed record FacialAcquisitionMetadata(
    DateTimeOffset CapturedAt,
    ushort DeviceType,
    byte SourceType,
    bool FullFrontalConfirmed,
    bool OpticalResolutionConfirmed,
    ushort Quality = 0
);

/// <summary>Describes the expected outcome of preparing a facial record.</summary>
public enum FacialPreparationStatus
{
    /// <summary>The facial record was prepared and its encoded metadata passed the profile.</summary>
    Prepared,

    /// <summary>Acquisition evidence needs operator review.</summary>
    AcquisitionReviewRequired,

    /// <summary>The supplied geometry or metadata does not meet the selected profile.</summary>
    ProfileRejected,

    /// <summary>The encoded image exceeded the permitted compression ratio.</summary>
    CompressionRejected,
}

/// <summary>Contains a prepared facial record and its acquisition evidence, or a review outcome.</summary>
/// <param name="Status">The preparation outcome.</param>
/// <param name="Record">The canonical facial record when prepared.</param>
/// <param name="Acquisition">The supplied camera/operator evidence.</param>
/// <param name="ProfileDiagnostics">Specific metadata findings.</param>
public sealed record FacialPreparationResult(
    FacialPreparationStatus Status,
    FaceRecord? Record,
    FacialAcquisitionMetadata Acquisition,
    ImmutableArray<BiometricProfileDiagnostic> ProfileDiagnostics
);

/// <summary>Prepares captured sRGB pixels as a PIV facial image record.</summary>
public interface IFacialRecordPreparer
{
    /// <summary>Encodes an attended capture as JPEG 2000, preserving its original geometry.</summary>
    /// <param name="srgbPixels">Tightly packed red, green, blue bytes in row-major order, starting at the top left.</param>
    /// <param name="width">Original width in pixels.</param>
    /// <param name="height">Original height in pixels.</param>
    /// <param name="acquisition">Camera metadata and operator-confirmed acquisition findings.</param>
    /// <returns>A canonical facial record or an expected acquisition/profile outcome.</returns>
    FacialPreparationResult Prepare(
        ReadOnlySpan<byte> srgbPixels,
        int width,
        int height,
        FacialAcquisitionMetadata acquisition
    );
}

/// <summary>Encodes real attended captures using the shared JPEG 2000 implementation.</summary>
public sealed class FacialRecordPreparer : IFacialRecordPreparer
{
    private static readonly object EncoderLock = new();
    private readonly ILogger<FacialRecordPreparer> logger;

    /// <summary>Creates a preparer with optional status-only diagnostics.</summary>
    /// <param name="logger">An application logger, or null for silent processing.</param>
    public FacialRecordPreparer(ILogger<FacialRecordPreparer>? logger = null) =>
        this.logger = logger ?? NullLogger<FacialRecordPreparer>.Instance;

    /// <summary>Encodes original sRGB pixels and prepares their INCITS 385 record.</summary>
    /// <param name="srgbPixels">Tightly packed sRGB pixels, starting at the top left.</param>
    /// <param name="width">Original width from one through 4096 pixels.</param>
    /// <param name="height">Original height from one through 4096 pixels.</param>
    /// <param name="acquisition">Explicit device, source, time, quality, and attended-acquisition findings.</param>
    /// <returns>A prepared record or an explicit review/rejection outcome.</returns>
    /// <exception cref="ArgumentNullException">The acquisition metadata is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Dimensions exceed supported memory bounds.</exception>
    /// <exception cref="ArgumentException">Pixel count differs from the dimensions.</exception>
    public FacialPreparationResult Prepare(
        ReadOnlySpan<byte> srgbPixels,
        int width,
        int height,
        FacialAcquisitionMetadata acquisition
    )
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        if (width is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (srgbPixels.Length != checked(width * height * 3))
            throw new ArgumentException(
                "Supply exactly three sRGB bytes per pixel.",
                nameof(srgbPixels)
            );
        if (!acquisition.FullFrontalConfirmed || !acquisition.OpticalResolutionConfirmed)
            return new(FacialPreparationStatus.AcquisitionReviewRequired, null, acquisition, []);
        if (width <= 420 || acquisition.SourceType is not (2 or 6) || acquisition.Quality > 100)
            return new(
                FacialPreparationStatus.ProfileRejected,
                null,
                acquisition,
                [new("FacialAcquisition", "Capture")]
            );

        // Skia owns the temporary pixel buffer. The caller retains its original
        // capture while CoreJ2K encodes this independent sRGB bitmap.
        using var colorSpace = SKColorSpace.CreateSrgb();
        using var image = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque, colorSpace)
        );
        var rgba = image.GetPixelSpan();
        for (var index = 0; index < width * height; index++)
        {
            rgba[index * 4] = srgbPixels[index * 3];
            rgba[index * 4 + 1] = srgbPixels[index * 3 + 1];
            rgba[index * 4 + 2] = srgbPixels[index * 3 + 2];
            rgba[index * 4 + 3] = 255;
        }
        byte[] encoded;
        lock (EncoderLock)
        {
            ImageFactory.Register(new SKBitmapImageCreator());
            var parameters = J2kImage.GetDefaultEncoderParameterList();
            // Twenty-four input bits per pixel at 1.6 output bits per pixel
            // corresponds to the whole-image PIV maximum ratio of 15:1.
            parameters["rate"] = 1.6f.ToString("F1", CultureInfo.InvariantCulture);
            parameters["lossless"] = "off";
            parameters["file_format"] = "on";
            parameters["Stiles"] = string.Create(CultureInfo.InvariantCulture, $"{width} {height}");
            encoded = J2kImage.ToBytes(image, parameters);
        }
        if ((long)encoded.Length * 15 < srgbPixels.Length)
            return new(
                FacialPreparationStatus.CompressionRejected,
                null,
                acquisition,
                [new("CompressionRatio", "ImageData")]
            );
        var face = new FaceImage(
            new FaceInformation(0, 0, 0, 0, 1, 0, 0),
            [],
            new FaceImageInformation(
                1,
                1,
                (ushort)width,
                (ushort)height,
                1,
                acquisition.SourceType,
                acquisition.DeviceType,
                acquisition.Quality
            ),
            ImmutableArray.Create(encoded)
        );
        var record = new FaceRecord([face]);
        var diagnostics = PivBiometricProfile.Validate(record);
        if (!diagnostics.IsEmpty)
            return new(FacialPreparationStatus.ProfileRejected, null, acquisition, diagnostics);
        logger.LogDebug("Facial record preparation completed.");
        return new(FacialPreparationStatus.Prepared, record, acquisition, []);
    }
}

/// <summary>Registers facial record preparation independently of fingerprint processing.</summary>
public static class FacialServiceCollectionExtensions
{
    /// <summary>Registers the captured-pixel JPEG 2000 facial record preparer.</summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same collection for chained registration.</returns>
    /// <exception cref="ArgumentNullException">The collection is null.</exception>
    public static IServiceCollection AddFacialRecordPreparation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IFacialRecordPreparer, FacialRecordPreparer>();
        return services;
    }
}
