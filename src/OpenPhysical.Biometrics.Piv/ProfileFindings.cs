// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;

// Turns a list of independent checks into diagnostics, preserving check order.
internal static class ProfileFindings
{
    internal static ImmutableArray<BiometricProfileDiagnostic> Failed(
        string field,
        params (bool Failed, string Code)[] checks
    ) =>
        checks
            .Where(check => check.Failed)
            .Select(check => new BiometricProfileDiagnostic(check.Code, field))
            .ToImmutableArray();
}
