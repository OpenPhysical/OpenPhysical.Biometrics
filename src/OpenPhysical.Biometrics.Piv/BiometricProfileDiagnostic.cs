// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

/// <summary>Identifies a failed biometric profile requirement.</summary>
/// <param name="Code">Stable diagnostic code.</param>
/// <param name="Field">The affected typed field path.</param>
public sealed record BiometricProfileDiagnostic(string Code, string Field);
