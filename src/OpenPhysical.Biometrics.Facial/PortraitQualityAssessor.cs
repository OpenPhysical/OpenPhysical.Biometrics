// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Measures frozen portrait evidence without modifying pixels or deciding acceptance.</summary>
public static class PortraitQualityAssessor
{
    /// <summary>Measures an automatically framed single face without claiming reviewed head boundaries.</summary>
    /// <param name="portrait">Source-supported portrait and its cached detections.</param>
    /// <returns>Advisory evidence or a typed source/geometry failure.</returns>
    public static Result<PortraitQualityMeasurements, FacialError> Assess(
        PortraitPreparationResult portrait
    ) =>
        portrait.Faces.Length == 1
            ? Assess(
                portrait,
                new ReviewedPortraitGeometry(
                    1,
                    portrait.SourceSha256,
                    portrait.SourceWidth,
                    portrait.SourceHeight,
                    portrait.Faces[0].Index
                )
            )
            : FacialErrors.InvalidPixelBuffer().Fail<PortraitQualityMeasurements>();

    /// <summary>Measures the prepared face; backdrop estimation requires explicit full-head protection.</summary>
    /// <param name="portrait">Exact prepared pixels and cached source-space detections.</param>
    /// <param name="review">Reviewed geometry bound to the same source.</param>
    /// <param name="protection">Reviewed complete head bounds and chin in prepared-portrait coordinates.</param>
    /// <returns>Finite advisory evidence or a typed source/geometry failure.</returns>
    public static Result<PortraitQualityMeasurements, FacialError> Assess(
        PortraitPreparationResult portrait,
        ReviewedPortraitGeometry review,
        Maybe<PortraitForegroundProtection> protection = default
    ) => Assess(portrait, review, protection, PortraitBackdropOptions.Default);

    /// <summary>Measures evidence using explicit application backdrop parameters.</summary>
    /// <param name="portrait">Exact prepared pixels and cached detections.</param>
    /// <param name="review">Source-bound reviewed geometry.</param>
    /// <param name="protection">Optional reviewed prepared-image head and shoulder protection.</param>
    /// <param name="options">Validated application diagnostic parameters.</param>
    /// <returns>Finite advisory evidence or a precise input failure.</returns>
    public static Result<PortraitQualityMeasurements, FacialError> Assess(
        PortraitPreparationResult portrait,
        ReviewedPortraitGeometry review,
        Maybe<PortraitForegroundProtection> protection,
        PortraitBackdropOptions options
    )
    {
        var valid = Validate(portrait, review, protection, options);
        if (valid.IsFailure)
            return valid.Error;
        var face = portrait.Faces.Single(value => value.Index == review.FaceIndex);
        var mask = FaceMask(face, portrait.Transform.Value);
        var rgb = portrait.RgbPixels.ToArray();
        try
        {
            var sharpness = PortraitLaplacian.Measure(rgb, 480, 640, mask);
            var backdrop = PortraitBackdrop.Measure(rgb, mask, protection, options);
            return new PortraitQualityMeasurements(
                portrait.SourceSha256,
                Sha256.HexDigest(rgb),
                sharpness,
                backdrop,
                Findings(sharpness, backdrop)
            );
        }
        finally
        {
            Array.Clear(rgb);
            Array.Clear(mask);
        }
    }

    private static UnitResult<FacialError> Validate(
        PortraitPreparationResult portrait,
        ReviewedPortraitGeometry review,
        Maybe<PortraitForegroundProtection> protection,
        PortraitBackdropOptions options
    )
    {
        var geometry = ReviewedGeometryCheck.Validate(
            review,
            portrait.SourceSha256,
            portrait.SourceWidth,
            portrait.SourceHeight,
            Maybe<int>.None
        );
        if (geometry.IsFailure)
            return geometry;
        if (
            portrait.Status is not (PortraitStatus.Prepared or PortraitStatus.ReviewRequired)
            || portrait.Transform.HasNoValue
            || portrait.RgbPixels.Length != 480 * 640 * 3
            || !options.IsValid
            || portrait.Faces.Count(value => value.Index == review.FaceIndex) != 1
            || portrait.Faces.Any(value =>
                value.Landmarks.Length != 478
                || value.Landmarks.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))
            )
            || protection.Match(InvalidProtection, () => false)
        )
            return UnitResult.Failure(FacialErrors.InvalidPixelBuffer());
        return UnitResult.Success<FacialError>();
    }

    private static bool InvalidProtection(PortraitForegroundProtection p) =>
        p.Left < 0
        || p.Top < 0
        || p.Right > 480
        || p.Bottom > 640
        || p.Left >= p.Right
        || p.Top >= p.Bottom
        || p.ChinY is < 0 or > 640;

    private static byte[] FaceMask(PortraitFace face, PortraitTransform transform)
    {
        var points = face.Landmarks.Select(value => transform.Map(new(value.X, value.Y))).ToArray();
        var left = points.Min(value => value.X);
        var right = points.Max(value => value.X);
        var top = points.Min(value => value.Y);
        var bottom = points.Max(value => value.Y);
        var rx = (right - left) / 2;
        var ry = (bottom - top) / 2;
        var mask = new byte[480 * 640];
        if (rx <= 0 || ry <= 0)
            return mask;
        for (var y = 0; y < 640; y++)
        for (var x = 0; x < 480; x++)
            if (
                Math.Pow((x + .5 - (left + rx)) / rx, 2) + Math.Pow((y + .5 - (top + ry)) / ry, 2)
                <= 1
            )
                mask[y * 480 + x] = 1;
        return mask;
    }

    private static ImmutableArray<PortraitFinding> Findings(
        Maybe<double> sharpness,
        PortraitBackdropMeasurements backdrop
    )
    {
        var findings = ImmutableArray.CreateBuilder<PortraitFinding>();
        if (sharpness.HasNoValue)
            findings.Add(
                new(
                    "SharpnessNotAssessable",
                    "The face region has insufficient detail for a focus measurement."
                )
            );
        if (!backdrop.EstimatedCandidate)
            findings.Add(
                new(
                    "BackdropReviewRequired",
                    "Review the background and illumination against the portrait."
                )
            );
        return findings.ToImmutable();
    }
}
