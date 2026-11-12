// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;

internal static class ProspectiveFailure
{
    internal static Result<string, DesktopError> Classify(DesktopError error) =>
        error.Code switch
        {
            "QualityImageNotAssessable" or "Capture.Nfiq1NotAssessable" => "ImageNotAssessable",
            "Capture.ContactTooLight"
            or "Capture.UnclearContact"
            or "Capture.NoFinger"
            or "Capture.MultipleFingers" => "ContactRejected",
            "Capture.NoReliableArea" => "NoReliableArea",
            "capture.AmbiguousTissueRegion" => "AmbiguousTissueRegion",
            _ => Result.Failure<string, DesktopError>(error),
        };
}
