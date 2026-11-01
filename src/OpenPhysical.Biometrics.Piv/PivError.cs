// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

/// <summary>Describes a failed PIV operation without exposing biometric or identity contents.</summary>
/// <param name="Code">Stable, machine-readable error identifier; see <see cref="PivErrorCodes"/>.</param>
/// <param name="Message">Explanation of the failed check.</param>
public sealed record PivError(string Code, string Message);
