// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Applies capture checks independently of inspection-image rendering.</summary>
public static class PortraitCaptureChecks
{
    /// <summary>Reassesses the cached mouth measurement while retaining exact image pixels and framing.</summary>
    /// <param name="portrait">A source-supported inspection portrait and its immutable checks.</param>
    /// <param name="mouthPolicy">New application tolerance, applied without inference or image rendering.</param>
    /// <returns>A new assessment sharing image buffers, or a missing-inspection-image failure.</returns>
    public static Result<PortraitPreparationResult, FacialError> Reassess(
        PortraitPreparationResult portrait,
        PortraitMouthPolicy mouthPolicy
    )
    {
        if (!PortraitInspectionData.IsUsable(portrait))
            return new FacialError(
                FacialErrorCodes.CaptureReviewRequired,
                "Retake the photo with your whole head and the surrounding background in view."
            );
        var findings = portrait
            .Findings.Where(f => f.Code is not "MouthOpen" and not "MouthUnassessable")
            .ToImmutableArray();
        var mouth = PortraitMouth.Assess(portrait.MouthMeasurement, mouthPolicy);
        if (mouth.IsFailure)
            findings = findings.Add(mouth.Error);
        return portrait with
        {
            MouthPolicy = mouthPolicy,
            Findings = findings,
            Status =
                portrait.Status is PortraitStatus.Prepared or PortraitStatus.ReviewRequired
                && !findings.Any(f =>
                    f.Code is not "EstimatedHeadBoundary" and not "BackgroundNotRemoved"
                )
                    ? PortraitStatus.Prepared
                    : PortraitStatus.ReviewRequired,
        };
    }

    internal static PortraitCaptureAssessment Assess(
        PortraitFace face,
        PortraitFeatures features,
        PortraitMouthPolicy mouthPolicy
    )
    {
        var findings = PortraitSourcePose.Findings(face, features);
        var measurement = PortraitMouth.Measure(face);
        var mouth = measurement.IsFailure
            ? UnitResult.Failure(measurement.Error)
            : PortraitMouth.Assess(measurement.Value, mouthPolicy);
        return new(
            mouth.IsFailure ? findings.Add(mouth.Error) : findings,
            measurement.IsSuccess ? measurement.Value : Maybe<PortraitMouthMeasurement>.None
        );
    }
}
