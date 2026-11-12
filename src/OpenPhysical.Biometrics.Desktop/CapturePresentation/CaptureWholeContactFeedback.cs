// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class CaptureWholeContactFeedback
{
    internal static Maybe<DesktopError> For(Maybe<Fs50WholeContactProof> proof) =>
        proof.Bind(value =>
            value.Record.Decision switch
            {
                Fs50WholeContactDecision.StaticExterior => new DesktopError(
                    "Capture.StaticExterior",
                    "Clean the sensor surface, then place your finger again."
                ),
                Fs50WholeContactDecision.UnresolvedExterior => new DesktopError(
                    "Capture.UnresolvedExterior",
                    "The contact is unclear. Clean the sensor or reposition your finger and try again."
                ),
                Fs50WholeContactDecision.Unavailable => new DesktopError(
                    "Capture.PresentationUnresolved",
                    "Clean the sensor or reposition your finger and try again."
                ),
                _ => Maybe<DesktopError>.None,
            }
        );
}
