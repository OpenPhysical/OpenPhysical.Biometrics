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

    /// <summary>Prepares one reviewed portrait without duplicating CLI or desktop readiness rules.</summary>
    /// <param name="portrait">The exact prepared pixels and source-bound head measurements.</param>
    /// <param name="acquisition">Explicit attended-capture findings.</param>
    /// <returns>A canonical record or the geometry/acquisition review still required.</returns>
    public Result<FacialPreparationResult, FacialError> Prepare(
        PortraitPreparationResult portrait,
        FacialAcquisitionMetadata acquisition
    ) => Prepare(portrait, acquisition, FacialPreparationPolicy.StrictPiv);

    /// <inheritdoc/>
    public Result<FacialPreparationResult, FacialError> Prepare(
        PortraitPreparationResult portrait,
        FacialAcquisitionMetadata acquisition,
        FacialPreparationPolicy policy
    )
    {
        var ready = FacialPortraitReadiness.Check(portrait, policy);
        if (ready.IsFailure)
            return Outcome(
                FacialPreparationStatus.GeometryReviewRequired,
                acquisition,
                [new(ready.Error.Code, "Portrait")]
            );
        var applied =
            portrait.Status == PortraitStatus.ReviewRequired ? policy.CaptureOverride : default;
        var evidence = applied.HasValue
            ? acquisition with
            {
                FullFrontalConfirmed = false,
            }
            : acquisition;
        return PreparePixels(
                portrait.RgbPixels.AsSpan(),
                PortraitOutput.Width,
                PortraitOutput.Height,
                evidence,
                policy,
                applied.HasValue
            )
            .Map(result => result with { CaptureOverride = applied });
    }

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
    ) =>
        PreparePixels(
            srgbPixels,
            width,
            height,
            acquisition,
            FacialPreparationPolicy.StrictPiv,
            false
        );

    private Result<FacialPreparationResult, FacialError> PreparePixels(
        ReadOnlySpan<byte> srgbPixels,
        int width,
        int height,
        FacialAcquisitionMetadata acquisition,
        FacialPreparationPolicy policy,
        bool allowUnconfirmedFrontal
    )
    {
        if (width is < 1 or > 4096 || height is < 1 or > 4096)
            return FacialErrors.InvalidDimensions().Fail<FacialPreparationResult>();
        if (srgbPixels.Length != width * height * 3)
            return FacialErrors.InvalidPixelBuffer().Fail<FacialPreparationResult>();
        if (
            (!acquisition.FullFrontalConfirmed && !allowUnconfirmedFrontal)
            || (
                policy.RequireOpticalResolutionConfirmation
                && !acquisition.OpticalResolutionConfirmed
            )
        )
            return Outcome(FacialPreparationStatus.AcquisitionReviewRequired, acquisition, []);
        var metadata = PivBiometricProfile.Validate(Information(width, height, acquisition));
        if (!metadata.IsEmpty)
            return Outcome(FacialPreparationStatus.ProfileRejected, acquisition, metadata);
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
            Information(width, height, acquisition),
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

    private static FaceImageInformation Information(
        int width,
        int height,
        FacialAcquisitionMetadata acquisition
    ) =>
        new(
            1,
            1,
            (ushort)width,
            (ushort)height,
            1,
            acquisition.SourceType,
            acquisition.DeviceType,
            acquisition.Quality
        );

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
