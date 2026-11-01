// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats.Cbeff;

// The fixed inputs of one signing run, shared by every length-resolution attempt.
internal sealed record BiometricSigningContext(
    CbeffRecord Record,
    BiometricIdentity Identity,
    IBiometricSignatureProvider Signer,
    IBiometricSignatureVerifier Verifier,
    CancellationToken CancellationToken
)
{
    // The block length is itself signed, so each length requires a fresh candidate header.
    internal CbeffRecord Candidate(ushort signatureLength) =>
        new(
            Record.Header.WithSignatureLength(signatureLength),
            Record.BiometricData,
            ImmutableArray<byte>.Empty
        );
}
