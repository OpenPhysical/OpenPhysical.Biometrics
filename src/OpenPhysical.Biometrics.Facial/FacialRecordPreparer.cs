// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Piv;

/// <summary>Encodes real attended captures using the shared JPEG 2000 implementation.</summary>
public sealed class FacialRecordPreparer : IFacialRecordPreparer
{
    private readonly ILogger<FacialRecordPreparer> logger;

    /// <summary>Creates a preparer with status-only diagnostics.</summary>
    /// <param name="logger">An application logger.</param>
    public FacialRecordPreparer(ILogger<FacialRecordPreparer> logger) => this.logger = logger;

    /// <summary>Encodes original sRGB pixels and prepares their INCITS 385 record.</summary>
    /// <param name="srgbPixels">Tightly packed sRGB pixels, starting at the top left.</param>
    /// <param name="width">Original width from one through 4096 pixels.</param>
    /// <param name="height">Original height from one through 4096 pixels.</param>
    /// <param name="acquisition">Explicit device, source, time, quality, and attended-acquisition findings.</param>
    /// <returns>A prepared record or an explicit review/rejection outcome, or a failure for invalid dimensions or pixels.</returns>
    public Result<FacialPreparationResult, FacialError> Prepare(
        ReadOnlySpan<byte> srgbPixels,
        int width,
        int height,
        FacialAcquisitionMetadata acquisition
    )
    {
        if (width is < 1 or > 4096 || height is < 1 or > 4096)
            return FacialErrors.InvalidDimensions().Fail<FacialPreparationResult>();
        if (srgbPixels.Length != width * height * 3)
            return FacialErrors.InvalidPixelBuffer().Fail<FacialPreparationResult>();
        if (!acquisition.FullFrontalConfirmed || !acquisition.OpticalResolutionConfirmed)
            return Outcome(FacialPreparationStatus.AcquisitionReviewRequired, acquisition, []);
        if (width <= 420 || acquisition.SourceType is not (2 or 6) || acquisition.Quality > 100)
            return Outcome(
                FacialPreparationStatus.ProfileRejected,
                acquisition,
                [new("FacialAcquisition", "Capture")]
            );
        var encoded = PortraitEncoding.EncodeJpeg2000(srgbPixels, width, height);
        if (encoded.IsSuccess)
            return Profile(encoded.Value, width, height, acquisition);
        return
            encoded.Error.Code
                is FacialErrorCodes.CompressionLimit
                    or FacialErrorCodes.EncodedDimensions
            ? Outcome(
                FacialPreparationStatus.CompressionRejected,
                acquisition,
                [new("CompressionRatio", "ImageData")]
            )
            : encoded.Error.Fail<FacialPreparationResult>();
    }

    private Result<FacialPreparationResult, FacialError> Profile(
        byte[] encoded,
        int width,
        int height,
        FacialAcquisitionMetadata acquisition
    )
    {
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
            return new FacialPreparationResult(
                FacialPreparationStatus.ProfileRejected,
                Maybe<FaceRecord>.None,
                acquisition,
                diagnostics
            ).Succeed();
        logger.LogDebug("Facial record preparation completed.");
        return new FacialPreparationResult(
            FacialPreparationStatus.Prepared,
            record,
            acquisition,
            []
        ).Succeed();
    }

    private static Result<FacialPreparationResult, FacialError> Outcome(
        FacialPreparationStatus status,
        FacialAcquisitionMetadata acquisition,
        ImmutableArray<BiometricProfileDiagnostic> diagnostics
    ) =>
        new FacialPreparationResult(
            status,
            Maybe<FaceRecord>.None,
            acquisition,
            diagnostics
        ).Succeed();
}
