// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;

namespace OpenPhysical.Biometrics.Desktop;

internal static class StudioVerificationPresentation
{
    internal static ImmutableArray<string> SetupProperties { get; } =
    [
        nameof(StudioVerificationSetupViewModel.PrimaryAction),
        nameof(StudioVerificationSetupViewModel.ShowPairing),
        nameof(StudioVerificationSetupViewModel.ShowOperators),
        nameof(StudioVerificationSetupViewModel.Ready),
        nameof(StudioVerificationSetupViewModel.Operators),
        nameof(StudioVerificationSetupViewModel.CanEdit),
        nameof(StudioVerificationSetupViewModel.ShowSetupAction),
        nameof(StudioVerificationSetupViewModel.ClockSetupBlocked),
    ];

    internal static string SetupAction(StudioVerificationState state) =>
        !state.Paired ? "Connect to server"
        : !state.SignedIn ? (state.Operators.IsEmpty ? "Reload sign-in identities" : "Sign in")
        : !state.ConfigurationReady ? "Update card-check settings"
        : !state.ClockReady ? "Trusted time setup required"
        : "Refresh readers and settings";

    internal static string SetupStatus(
        StudioVerificationState state,
        bool clockBlocked,
        string action
    ) =>
        clockBlocked
            ? "Your administrator needs to configure trusted time and restart Studio before checking cards. See Administrator setup below."
        : state.Paired && !state.SignedIn && state.Operators.IsEmpty
            ? "Your administrator needs to install your sign-in identity on this Mac. Then reload saved settings."
        : state.Ready ? "Ready. Insert a card, then choose the check you need."
        : action + " to continue.";

    internal static Result<byte[], DesktopError> Pin(string value, bool required)
    {
        if (!required)
            return value.Length == 0
                ? Array.Empty<byte>()
                : new DesktopError("Verification.Pin", "This check does not use a PIN.");
        return value.Length is >= 6 and <= 8 && value.All(char.IsAsciiDigit)
            ? Encoding.ASCII.GetBytes(value)
            : new DesktopError(
                "Verification.Pin",
                "Enter a PIN of 6 to 8 digits. It will be sent once."
            );
    }

    internal static string Summary(CredentialVerificationResult result) =>
        result.Decision switch
        {
            CredentialVerificationDecision.Accepted => "Card verified. All required checks passed.",
            CredentialVerificationDecision.Rejected =>
                "Card not verified. A required check failed.",
            _ => "Card could not be verified. Required evidence or current settings are missing.",
        };

    internal static string Details(CredentialVerificationResult result) =>
        string.Join(
            Environment.NewLine,
            new[]
            {
                Line("Card proof", result.Cryptographic),
                Line("Holder", result.Holder),
                Line("Revocation", result.Status),
                Line("Issuer trust", result.Trust),
                Line("Signed policy", result.Policy),
            }
        );

    private static string Line(string label, VerificationCheck check) =>
        label
        + ": "
        + check.State
        + (
            check.ReasonCodes.IsEmpty
                ? string.Empty
                : " (" + string.Join(", ", check.ReasonCodes) + ")"
        );
}
