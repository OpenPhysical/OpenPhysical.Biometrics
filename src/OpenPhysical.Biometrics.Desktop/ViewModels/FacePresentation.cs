// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

internal static class FacePresentation
{
    internal static readonly string[] Properties =
    [
        "QualificationRequired",
        "IsLive",
        "AutoCapture",
        "IsBusy",
        "HasCapture",
        "IsReviewing",
        "RequiresRetake",
        "CanOverride",
        "HasFailures",
        "FailureMessage",
        "Complete",
        "CanReset",
        "Progress",
        "Instructions",
        "ReviewConfirmed",
        "Details",
    ];

    internal static string Progress(FaceCaptureWorkflow workflow) =>
        workflow.Draft.Accepted.HasValue ? "Face accepted"
        : workflow.Draft.RequiresRetake ? "Retake needed"
        : workflow.Draft.Source.HasValue ? "Waiting for acceptance"
        : "Waiting for capture";

    internal static string Instructions(
        FaceCaptureWorkflow workflow,
        PersonEnrollmentViewModel person
    )
    {
        if (workflow.Problem.HasValue)
            return workflow.Problem.Value.Message;
        var draft = workflow.Draft;
        var retake = FaceRetakeGuidance.Retake(draft.Portrait);
        if (draft.RequiresRetake && retake.HasValue)
            return retake.Value;
        if (draft.Source.HasNoValue || workflow.Busy)
            return workflow.Guidance;
        if (draft.Accepted.HasValue)
            return "Face accepted.";
        var prefix = draft.CaptureOverride.HasValue ? "Override recorded. " : string.Empty;
        if (person.Create().IsFailure)
            return prefix + "Enter a name or label for this person.";
        if (!draft.Overrides.Ready(draft.Portrait))
            return prefix + "Retake with your whole head and the surrounding background in view.";
        return draft.ReviewConfirmed
            ? prefix + "Accept the face to continue."
            : prefix + "Confirm the portrait review before accepting the face.";
    }

    internal static string Details(FaceCaptureDraft draft, bool qualified) =>
        draft.Source.Match(
            source =>
                $"Native camera: {source.Mode}. Source: {source.Image.Width} × {source.Image.Height}."
                + (
                    qualified
                        ? "\nOptical measurement recorded for this camera and mode."
                        : "\nOptical measurement recommended for this camera and mode; none is recorded."
                )
                + draft.Quality.Match(
                    value =>
                        "\nSharpness variance: "
                        + value.MaskedLaplacianVariance.Match(
                            score =>
                                score.ToString(
                                    "0.###",
                                    System.Globalization.CultureInfo.InvariantCulture
                                ),
                            () => "not assessable"
                        )
                        + $" (advisory). Background: {(value.Backdrop.EstimatedCandidate ? "estimated candidate" : "undetermined")}.",
                    () => string.Empty
                )
                + PoseDetails(draft)
                + draft
                    .Portrait.Bind(value => value.Background)
                    .Match(
                        value =>
                            value.Fill.Match(
                                color =>
                                    $"\nBackground replaced using median RGB {color.Red}/{color.Green}/{color.Blue} from {color.SampleCount} original background pixels; {value.ReplacedPixelCount} pixels edited.",
                                () =>
                                    "\nBackground could not be separated; the original background is retained."
                            ),
                        () => string.Empty
                    ),
            () => string.Empty
        );

    private static string PoseDetails(FaceCaptureDraft draft) =>
        draft
            .Analysis.Bind(analysis =>
                analysis.Faces.Length == 1 ? analysis.Faces[0].Pose : default
            )
            .Match(
                pose =>
                    FormattableString.Invariant(
                        $"\nEstimated source pose: turn {pose.YawDegrees:0.0}°, up/down {pose.PitchDegrees:0.0}°, tilt {pose.RollDegrees:0.0}°. Virtual-camera field of view: {pose.VerticalFieldOfViewDegrees:0.#}° ({pose.PolicyId})."
                    ),
                () => string.Empty
            );
}
