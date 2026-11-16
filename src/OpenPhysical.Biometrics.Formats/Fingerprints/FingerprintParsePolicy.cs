// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Explicit coordinate interpretation while retaining original wire order and extension indices.</summary>
public enum FingerprintParsePolicy
{
    /// <summary>Rejects every minutia outside the declared image geometry.</summary>
    Strict,

    /// <summary>Retains out-of-area points with warnings for a caller's standards-defined comparison projection.</summary>
    PreserveOutOfBoundsMinutiae,
}
