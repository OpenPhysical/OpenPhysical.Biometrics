// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

// Builds the two PIV finger views with their minutiae limits and units.
internal static class PivEnrollmentRecordBuilder
{
    private const int MaximumMinutiae = 128;

    internal static Result<FingerprintRecord, FingerprintError> Build(
        FingerprintEnrollmentFinger primary,
        FingerprintEnrollmentFinger secondary,
        PivFingerprintRecordMetadata metadata
    ) =>
        CheckSelection(primary, secondary, metadata)
            .Bind(() => CheckCaptures(primary, secondary, metadata))
            .Bind(() => CreateView(primary))
            .Bind(first =>
                CreateView(secondary)
                    .Map(second => new FingerprintRecord(
                        metadata.ProductOwner,
                        metadata.ProductType,
                        (ushort)(0x8000 | metadata.EquipmentId),
                        metadata.Width,
                        metadata.Height,
                        FingerprintGeometry.NominalPixelsPerCentimeter,
                        FingerprintGeometry.NominalPixelsPerCentimeter,
                        [first, second]
                    ))
            );

    private static UnitResult<FingerprintError> CheckSelection(
        FingerprintEnrollmentFinger primary,
        FingerprintEnrollmentFinger secondary,
        PivFingerprintRecordMetadata metadata
    ) =>
        FingerprintChecks
            .Require(
                metadata.EquipmentAppendixFCertified,
                FingerprintErrorCodes.EquipmentCertification,
                "PIV enrollment requires Appendix F acquisition equipment."
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    primary.Finger != secondary.Finger,
                    FingerprintErrorCodes.DuplicateFingers,
                    "Select two different fingers."
                )
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    primary.Availability == FingerprintAvailability.Available
                        || secondary.Availability != FingerprintAvailability.Available,
                    FingerprintErrorCodes.PrimaryMustBeAvailable,
                    "Select the available finger as the primary finger."
                )
            );

    private static UnitResult<FingerprintError> CheckCaptures(
        FingerprintEnrollmentFinger primary,
        FingerprintEnrollmentFinger secondary,
        PivFingerprintRecordMetadata metadata
    )
    {
        var captures = new[] { primary.Capture, secondary.Capture }.Choose().ToArray();
        return FingerprintChecks
            .Require(
                captures.Length == 0
                    || (
                        captures.Max(capture => capture.Image.Width) == metadata.Width
                        && captures.Max(capture => capture.Image.Height) == metadata.Height
                    ),
                FingerprintErrorCodes.RecordDimensions,
                "Record dimensions must equal the largest original dimensions."
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    captures.All(capture =>
                        capture.EquipmentId == metadata.EquipmentId
                        && capture.EquipmentAppendixFCertified
                    ),
                    FingerprintErrorCodes.EquipmentMismatch,
                    "Capture equipment identifiers must agree with the record metadata."
                )
            );
    }

    // The selection is stable across runs. Coordinates are converted against each source
    // image, before the record header adopts the largest dimensions of the two source images.
    private static Result<FingerprintView, FingerprintError> CreateView(
        FingerprintEnrollmentFinger selection
    ) =>
        selection.Template.Match(
            template => CreateAvailableView(selection, template),
            () =>
                Result.Success<FingerprintView, FingerprintError>(
                    new FingerprintView(
                        (byte)selection.Finger,
                        0,
                        0,
                        selection.Availability == FingerprintAvailability.PermanentlyUnusable
                            ? (byte)254
                            : (byte)255,
                        [],
                        []
                    )
                )
        );

    private static Result<FingerprintView, FingerprintError> CreateAvailableView(
        FingerprintEnrollmentFinger selection,
        NBIS.FingerprintTemplate template
    ) =>
        selection
            .Capture.ToResult(MissingEvidence())
            .Bind(capture =>
                selection
                    .Quality.ToResult(MissingEvidence())
                    .Map(quality => BuildAvailableView(selection, template, capture, quality))
            );

    private static FingerprintError MissingEvidence() =>
        new(
            FingerprintErrorCodes.UnavailableCandidate,
            "An available finger requires its capture, template, and quality assessment."
        );

    private static FingerprintView BuildAvailableView(
        FingerprintEnrollmentFinger selection,
        NBIS.FingerprintTemplate template,
        FingerprintCapture capture,
        FingerprintQualityAssessment quality
    ) =>
        new(
            (byte)selection.Finger,
            0,
            (byte)capture.Impression,
            quality.PivQuality,
            template
                .Minutiae.OrderByDescending(point => point.Quality)
                .ThenBy(point => point.X)
                .ThenBy(point => point.Y)
                .ThenBy(point => point.Angle)
                .ThenBy(point => point.Type)
                .DistinctBy(point => (point.X, point.Y))
                .Take(MaximumMinutiae)
                .Select(point => FingerprintGeometry.ToRecord(point, template.Height))
                .ToImmutableArray(),
            []
        );
}
