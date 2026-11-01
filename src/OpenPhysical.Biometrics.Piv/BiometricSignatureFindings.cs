// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using CSharpFunctionalExtensions;

/// <summary>Contains the results supplied by the external signature and trust services.</summary>
/// <param name="Signature">Signature-integrity status.</param>
/// <param name="Envelope">CMS profile and signed-attribute conformance status.</param>
/// <param name="Trust">Certificate-path trust status.</param>
/// <param name="Revocation">Certificate-revocation status.</param>
/// <param name="VerifiedIdentity">Identity authenticated by the signature, when available.</param>
public sealed record BiometricSignatureFindings(
    ValidationStatus Signature,
    ValidationStatus Envelope,
    ValidationStatus Trust,
    ValidationStatus Revocation,
    Maybe<BiometricIdentity> VerifiedIdentity
);
