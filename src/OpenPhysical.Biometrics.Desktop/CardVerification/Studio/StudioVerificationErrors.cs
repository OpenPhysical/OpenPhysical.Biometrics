// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Workstation.Application;

namespace OpenPhysical.Biometrics.Desktop;

internal static class StudioVerificationErrors
{
    internal static DesktopError From(WorkstationError error) =>
        new(
            "Verification." + error.Code,
            error.Code switch
            {
                WorkstationErrorCodes.Unpaired =>
                    "Connect this computer using an invitation from your administrator.",
                WorkstationErrorCodes.NotAuthorized => "Sign in again before checking a card.",
                WorkstationErrorCodes.OutcomeUncertain =>
                    "The connection was interrupted. Reload saved settings before trying again.",
                _ => "This step could not finish. Check your connection or ask your administrator.",
            }
        );

    internal static DesktopError From(VerificationError error) =>
        new(
            "Verification." + error.Code,
            error.Code == VerificationErrorCodes.Cancelled
                ? "Card check cancelled."
                : "The card could not be verified. Check the card, reader and current signed settings."
        );
}
