// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class CapturePreviewGuidance
{
    internal const string Removal = "Lift your finger. If it is already off, clean the sensor.";

    internal static Maybe<DesktopError> Feedback(
        Maybe<Fs50ContactRegion> foreground,
        Maybe<Fs50WholeContactProof> proof
    ) =>
        foreground.Match(value => value.SpatialComponentCount > 1, () => false)
            ? Maybe.From(new DesktopError("Capture.MultipleFingers", "Place one finger."))
            : CaptureWholeContactFeedback
                .For(proof)
                .Or(foreground.Bind(value => CaptureContactFeedback.ForIssue(value.Issue)));

    internal static string WaitingForSensor(
        Maybe<Fs50ContactRegion> foreground,
        Maybe<DesktopError> feedback
    ) =>
        feedback.Match(
            error => error.Message,
            () =>
                foreground.Match(
                    region =>
                        region.SpatialComponentCount > 1 ? "Place one finger on the sensor."
                        : region.SpatialComponentCount == 1
                            ? "Lift your finger so the sensor can check its surface."
                        : "Checking the sensor surface. Leave it clear for a moment.",
                    () => "Leave the sensor clear for a moment."
                )
        );
}
