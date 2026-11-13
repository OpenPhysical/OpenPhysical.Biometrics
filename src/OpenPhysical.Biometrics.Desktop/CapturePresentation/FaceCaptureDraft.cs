// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;

internal sealed class FaceCaptureDraft
{
    internal Maybe<CameraFrame> Source { get; private set; }
    internal Maybe<PortraitAnalysis> Analysis { get; private set; }
    private Maybe<PortraitPreparationResult> portrait;
    internal FaceDraftOverride Overrides { get; } = new();
    internal Maybe<FacialCaptureOverride> CaptureOverride => Overrides.Current;
    internal bool RequiresRetake =>
        CaptureOverride.HasNoValue && FaceRetakeGuidance.Retake(Portrait).HasValue;
    internal Maybe<PortraitPreparationResult> Portrait
    {
        get => portrait;
        set
        {
            portrait = value;
            Overrides.Retain(value);
        }
    }
    internal Maybe<PortraitQualityMeasurements> Quality { get; set; }
    internal Maybe<FaceAcceptedCapture> Accepted { get; set; }
    internal bool ReviewConfirmed { get; set; }

    internal UnitResult<DesktopError> Begin(
        CameraFrame source,
        PortraitAnalysis analysis,
        FacePreparedReview review
    )
    {
        if (!Source.Match(value => ReferenceEquals(value, source), () => false))
            Freeze(source);
        Analysis = analysis;
        Portrait = review.Portrait;
        Quality = review.Quality;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Freeze(CameraFrame source)
    {
        Clear();
        Source = source;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> ClearReviewEvidence()
    {
        ReviewConfirmed = false;
        return Overrides.Clear();
    }

    internal UnitResult<DesktopError> ReplaceProcessing(
        PortraitAnalysis analysis,
        FacePreparedReview review
    )
    {
        Analysis = analysis;
        Portrait = review.Portrait;
        Quality = review.Quality;
        return ClearReviewEvidence();
    }

    internal UnitResult<DesktopError> Clear()
    {
        Source.Tap(value => value.Dispose());
        Overrides.Clear();
        Source = default;
        Analysis = default;
        Portrait = default;
        Quality = default;
        Accepted = default;
        ReviewConfirmed = false;
        return UnitResult.Success<DesktopError>();
    }
}
