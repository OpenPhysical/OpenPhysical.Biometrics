// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Workstation.Application.Devices;
using OpenPhysical.Workstation.Infrastructure.Security;
using OpenPhysical.Workstation.Infrastructure.Verification;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal static class StudioVerificationStateFixture
{
    internal static StudioVerificationChoice Managed { get; } =
        new(
            "OpenPhysical test card",
            CredentialVerificationProfiles.ManagedTestCardAuthentication,
            StudioVerificationKind.Card
        );
    internal static StudioVerificationChoice Pin { get; } =
        new(
            "Card and PIN",
            CredentialVerificationProfiles.PinAuthentication,
            StudioVerificationKind.Pin
        );
    internal static StudioVerificationChoice Fingerprint { get; } =
        new(
            "Card and fingerprint",
            CredentialVerificationProfiles.CardAuthentication,
            StudioVerificationKind.PivFingerprint
        );
    internal static StudioVerificationState Ready { get; } =
        new(
            true,
            true,
            true,
            [new OperatorIdentityOption("protected-operator", "Fixture operator")],
            [Managed, Pin, Fingerprint],
            [new WorkstationReader(Guid.NewGuid(), "Fixture reader")],
            [new WorkstationCaptureDevice("fixture-scanner", "Fixture scanner")]
        )
        {
            ClockReady = true,
        };

    internal static CredentialVerificationResult MissingStatus()
    {
        var passed = new VerificationCheck(true, VerificationCheckState.Passed, []);
        var missing = new VerificationCheck(
            true,
            VerificationCheckState.Missing,
            ["status.unavailable"]
        );
        return new(
            passed,
            passed,
            missing,
            passed,
            passed,
            [new string('a', 64)],
            DateTimeOffset.UtcNow.AddMinutes(1)
        );
    }
}
