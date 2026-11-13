// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>One ceremony selected from authenticated configuration.</summary>
/// <param name="Label">Human-readable operation name.</param>
/// <param name="ProfileId">Exact signed policy context.</param>
/// <param name="Kind">Local card, PIV fingerprint or TWIC fingerprint ceremony.</param>
public sealed record StudioVerificationChoice(
    string Label,
    string ProfileId,
    StudioVerificationKind Kind
)
{
    /// <summary>Whether an explicit holder PIN is needed.</summary>
    public bool NeedsPin =>
        Kind is StudioVerificationKind.Pin or StudioVerificationKind.PivFingerprint;

    /// <summary>Whether an enrolled-finger comparison is needed.</summary>
    public bool NeedsScanner =>
        Kind is StudioVerificationKind.PivFingerprint or StudioVerificationKind.TwicFingerprint;

    /// <summary>The local card ceremony, separate from guided fingerprint capture.</summary>
    public LocalCardVerificationMethod Method =>
        Kind == StudioVerificationKind.Pin
            ? LocalCardVerificationMethod.PinAuthentication
            : LocalCardVerificationMethod.CardAuthentication;

    /// <summary>Shows only the operation name in selection controls.</summary>
    /// <returns>The human-readable selection label.</returns>
    public override string ToString() => Label;
}

/// <summary>Operations supported by the existing verifier.</summary>
public enum StudioVerificationKind
{
    /// <summary>Prove the card's current private key.</summary>
    Card,

    /// <summary>Prove the card and verify an explicitly entered PIN once.</summary>
    Pin,

    /// <summary>Compare an enrolled PIV finger.</summary>
    PivFingerprint,

    /// <summary>Compare an enrolled TWIC finger.</summary>
    TwicFingerprint,
}
