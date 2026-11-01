// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Examples;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenPhysical.Biometrics.Facial;

/// <summary>Shows capture preparation and record creation with explicit review evidence.</summary>
public static class PortraitExample
{
    /// <summary>Prepares reviewed pixels and returns the canonical record outcome.</summary>
    /// <param name="capture">Original encoded capture, including orientation and color metadata.</param>
    /// <param name="review">Head boundaries reviewed against this exact capture.</param>
    /// <param name="acquisition">Camera and operator findings from the capture session.</param>
    /// <returns>The prepared record, the acquisition/profile outcome needing review, or a failure.</returns>
    public static Result<FacialPreparationResult, FacialError> PrepareRecord(
        ReadOnlySpan<byte> capture,
        ReviewedPortraitGeometry review,
        FacialAcquisitionMetadata acquisition
    )
    {
        // Keep one service for a capture session so its native model sessions can be reused.
        using var portraits = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);

        // The capture station presents findings to its operator. Coordinates and image
        // samples stay in the result, while operational logs carry status codes.
        return portraits
            .Prepare(capture, review)
            .Ensure(
                portrait =>
                    portrait.Status == PortraitStatus.Prepared
                    && portrait.Measurements.Value.ReviewedHeadBoundary,
                new FacialError(
                    "ReviewRequired",
                    "Review the capture and its head boundaries before issuance."
                )
            )
            .Bind(portrait =>
                new FacialRecordPreparer(NullLogger<FacialRecordPreparer>.Instance).Prepare(
                    portrait.RgbPixels.AsSpan(),
                    480,
                    640,
                    acquisition
                )
            );
    }
}
