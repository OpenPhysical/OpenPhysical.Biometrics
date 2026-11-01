// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using MatchingTemplate = OpenPhysical.Biometrics.NBIS.FingerprintTemplate;

/// <summary>Converts an explicitly selected canonical fingerprint view into an NBIS matching template.</summary>
public static class FingerprintRecordAdapter
{
    private const string TemplateProvenance =
        "OpenPhysical INCITS 378-2004 to NBIS XYT; nominal 500 ppi";

    /// <summary>Converts one finger and view while preserving the source image scale.</summary>
    /// <param name="record">An already parsed or authenticated fingerprint record.</param>
    /// <param name="finger">The finger to select.</param>
    /// <param name="viewNumber">The selected view identifier, from zero through fifteen.</param>
    /// <returns>An immutable nominal 500 ppi NBIS template with height-minus-row coordinates, or an error with one of <see cref="FingerprintErrorCodes"/>.</returns>
    /// <remarks>Pixel scale and minutia type are retained. INCITS two-degree angles become whole degrees
    /// with a half-turn direction reversal. Every selected point is copied into the matching template.
    /// Views exceeding 150 points require reported quality for Bozorth3's point ranking.</remarks>
    public static Result<MatchingTemplate, FingerprintError> ToMatchingTemplate(
        this FingerprintRecord record,
        FingerPosition finger,
        byte viewNumber
    ) =>
        CheckSelection(record, finger, viewNumber)
            .Bind(() => SelectView(record, finger, viewNumber))
            .Check(view => CheckView(view, record))
            .Bind(view => BuildTemplate(record, view));

    private static UnitResult<FingerprintError> CheckSelection(
        FingerprintRecord record,
        FingerPosition finger,
        byte viewNumber
    ) =>
        FingerprintChecks
            .Require(
                Enum.IsDefined(finger),
                FingerprintErrorCodes.InvalidFinger,
                "The finger position is not an ANSI finger code."
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    viewNumber <= 15,
                    FingerprintErrorCodes.InvalidViewNumber,
                    "The view number must be from zero through fifteen."
                )
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    record.XResolution == FingerprintGeometry.NominalPixelsPerCentimeter
                        && record.YResolution == FingerprintGeometry.NominalPixelsPerCentimeter,
                    FingerprintErrorCodes.UnsupportedResolution,
                    "Matching requires isotropic 197 pixels/cm, the nominal 500 ppi encoding."
                )
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    !record.Views.IsDefault,
                    FingerprintErrorCodes.UninitializedViews,
                    "The record requires initialized fingerprint views."
                )
            );

    private static Result<FingerprintView, FingerprintError> SelectView(
        FingerprintRecord record,
        FingerPosition finger,
        byte viewNumber
    )
    {
        var selected = record
            .Views.Where(view =>
                view.FingerPosition == (byte)finger && view.ViewNumber == viewNumber
            )
            .Take(2)
            .ToArray();
        return selected.Length == 1
            ? selected[0]
            : FingerprintChecks.Fail<FingerprintView>(
                FingerprintErrorCodes.ViewSelection,
                "Select exactly one existing finger and view."
            );
    }

    private static UnitResult<FingerprintError> CheckView(
        FingerprintView view,
        FingerprintRecord record
    ) =>
        FingerprintChecks
            .Require(
                view.Quality is not (254 or 255),
                FingerprintErrorCodes.FingerUnavailable,
                "The selected finger is unavailable."
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    view.Quality <= 100
                        && !view.Minutiae.IsDefault
                        && view.Minutiae.Length <= byte.MaxValue,
                    FingerprintErrorCodes.InvalidView,
                    "The selected view has invalid quality or minutia count."
                )
            )
            // Bozorth3 ranks at most 150 points. When quality is unreported,
            // the caller selects the subset explicitly.
            .Bind(() =>
                FingerprintChecks.Require(
                    view.Minutiae.Length <= 150 || view.Minutiae.All(point => point.Quality != 0),
                    FingerprintErrorCodes.UnreportedQualityLimit,
                    "Select at most 150 minutiae when their quality is unreported."
                )
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    view.Minutiae.All(point =>
                        FingerprintGeometry.IsValid(point, record.Width, record.Height)
                    ),
                    FingerprintErrorCodes.InvalidMinutia,
                    "The selected view contains invalid INCITS 378 minutiae."
                )
            );

    private static Result<MatchingTemplate, FingerprintError> BuildTemplate(
        FingerprintRecord record,
        FingerprintView view
    ) =>
        FingerprintEdge.Try(
            () =>
                new MatchingTemplate(
                    record.Width,
                    record.Height,
                    view.Minutiae.Select(point =>
                        FingerprintGeometry.ToMatching(point, record.Height)
                    ),
                    TemplateProvenance
                ),
            FingerprintErrorCodes.InvalidTemplate,
            "The matching template rejected the selected view."
        );
}
