// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using MatchingTemplate = OpenPhysical.Biometrics.NBIS.FingerprintTemplate;

/// <summary>Selects and converts the canonical minutiae shared by export, display and PIV issuance.</summary>
public static class FingerprintViewFactory
{
    private const int MaximumMinutiae = 128;

    /// <summary>Creates a view with at most 128 quality-ranked, distinct minutia positions.</summary>
    /// <param name="template">Immutable NBIS extraction in height-minus-row coordinates.</param>
    /// <param name="quality">The original image's NFIQ 1 assessment.</param>
    /// <param name="finger">Explicit ANSI finger position.</param>
    /// <param name="viewNumber">View identifier from zero through fifteen.</param>
    /// <param name="impression">The plain acquisition method.</param>
    /// <returns>A canonical top-left-origin view, or an invalid metadata failure.</returns>
    /// <remarks>Angles reverse the NBIS ridge direction and round down to two-degree INCITS units.
    /// Point ordering and duplicate selection are deterministic. Source data is unchanged.</remarks>
    public static Result<FingerprintView, FingerprintError> Create(
        MatchingTemplate template,
        FingerprintQualityAssessment quality,
        FingerPosition finger,
        byte viewNumber,
        FingerprintImpression impression
    ) =>
        CheckMetadata(finger, viewNumber, impression)
            .Map(() =>
                new FingerprintView(
                    (byte)finger,
                    viewNumber,
                    (byte)impression,
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
                )
            );

    private static UnitResult<FingerprintError> CheckMetadata(
        FingerPosition finger,
        byte viewNumber,
        FingerprintImpression impression
    ) =>
        FingerprintChecks
            .Finger(finger)
            .Bind(() => FingerprintChecks.ViewNumber(viewNumber))
            .Bind(() => FingerprintChecks.Impression(impression));
}
