// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal static class CapturePolicyValidation
{
    internal static Result<CapturePolicyParts, DesktopError> Create(
        EnhancementSettings enhancement,
        Fs50EvidenceOptions evidence,
        Fs50EmptySensorOptions empty,
        int nfiq1,
        int nfiq2,
        int margin
    ) =>
        enhancement is null
            ? new DesktopError("Settings.Policy", "Enhancement settings are required.")
            : Fs50EvidencePolicy
                .Create(evidence)
                .MapError(Error)
                .Bind(contact =>
                    Fs50EmptySensorPolicy
                        .Create(empty)
                        .MapError(Error)
                        .Bind(sensor =>
                            FingerprintQualitySelectionPolicy
                                .Create(nfiq1, nfiq2)
                                .MapError(error => new DesktopError(error.Code, error.Message))
                                .Bind(quality =>
                                    Complete(enhancement, contact, sensor, quality, margin)
                                )
                        )
                );

    private static Result<CapturePolicyParts, DesktopError> Complete(
        EnhancementSettings enhancement,
        Fs50EvidencePolicy evidence,
        Fs50EmptySensorPolicy empty,
        FingerprintQualitySelectionPolicy quality,
        int margin
    ) =>
        Fs50ContactContourPolicy
            .Create(
                new()
                {
                    ContextMargin = margin,
                    ModifiedBoundaryGuard = evidence.Options.InnerGuard,
                }
            )
            .MapError(_ => new DesktopError(
                "Settings.ContextMargin",
                "Image context margin must be from 0 to 128 pixels."
            ))
            .Bind(contour =>
                Fs50WholeContactPolicy
                    .Create(new(), evidence, empty, FingerCapturePolicy.Default)
                    .MapError(Error)
                    .Map(whole => new CapturePolicyParts(
                        enhancement,
                        evidence,
                        empty,
                        quality,
                        contour,
                        whole
                    ))
            );

    internal static DesktopError Error(CaptureError error) => new(error.Code, error.Message);
}
