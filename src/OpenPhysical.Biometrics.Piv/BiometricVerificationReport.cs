// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

/// <summary>Separates the external cryptographic findings from biometric identity binding.</summary>
/// <param name="SignatureFindings">The findings supplied by the caller's verification service.</param>
/// <param name="IdentityBinding">Whether the authenticated attributes match the CBEFF header and expected card identity.</param>
public sealed record BiometricVerificationReport(
    BiometricSignatureFindings SignatureFindings,
    ValidationStatus IdentityBinding
);
