// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Contains a completed object or an explicit expected signing outcome.</summary>
/// <param name="Failure">The outcome of producing a self-consistent signed object.</param>
/// <param name="Record">The completed record when signing succeeds.</param>
/// <param name="Verification">Verification findings when the external verifier was called.</param>
public sealed record BiometricSigningResult(
    SignatureFailure Failure,
    Maybe<CbeffRecord> Record,
    Maybe<BiometricVerificationReport> Verification
)
{
    /// <summary>Gets whether signing and external integrity verification completed.</summary>
    public bool IsSuccess => Failure == SignatureFailure.None;
}
