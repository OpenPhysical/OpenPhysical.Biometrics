// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

namespace OpenPhysical.Biometrics.Desktop;

internal static class CaptureContactFeedback
{
    internal static Maybe<DesktopError> ForIssue(Fs50ContactIssue issue) =>
        issue switch
        {
            Fs50ContactIssue.None => Maybe<DesktopError>.None,
            Fs50ContactIssue.TooLight => new DesktopError(
                "Capture.ContactTooLight",
                "Contact is too light. Clean the sensor or try repositioning your finger; dry skin can cause this too."
            ),
            Fs50ContactIssue.InsufficientRidgeEvidence => new DesktopError(
                "Capture.UnclearContact",
                "Finger contact is unclear. Clean the sensor or reposition your finger."
            ),
            _ => new DesktopError(
                "Capture.ContactEvidence",
                "Cannot evaluate finger contact. Try another capture."
            ),
        };

    internal static UnitResult<DesktopError> Validate(Fs50ContactIssue issue) =>
        ForIssue(issue).Match(error => UnitResult.Failure(error), UnitResult.Success<DesktopError>);

    internal static UnitResult<DesktopError> Validate(Fs50ContactDiagnostics diagnostics)
    {
        var issue = Validate(diagnostics.Issue);
        if (issue.IsFailure)
            return issue;
        if (diagnostics.AcceptedArea <= 0)
            return UnitResult.Failure(
                new DesktopError(
                    "Capture.NoFinger",
                    "Place the selected finger on the scanner, then capture again."
                )
            );
        return diagnostics.ComponentCount == 1 && diagnostics.SpatialComponentCount == 1
            ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(
                new DesktopError(
                    "Capture.MultipleFingers",
                    "More than one finger region was found. Place only the selected finger on the scanner."
                )
            );
    }
}
