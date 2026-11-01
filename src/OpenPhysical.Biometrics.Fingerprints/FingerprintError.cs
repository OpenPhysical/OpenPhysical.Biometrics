// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>Describes a failed fingerprint operation without exposing biometric contents.</summary>
/// <param name="Code">Stable, machine-readable error identifier; see <see cref="FingerprintErrorCodes"/>.</param>
/// <param name="Message">Explanation of the failed check.</param>
public sealed record FingerprintError(string Code, string Message);
