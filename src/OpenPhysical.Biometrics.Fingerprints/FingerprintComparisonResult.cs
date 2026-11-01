// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>A comparison result bound to the exact application policy used.</summary>
/// <param name="RawScore">The unmodified Bozorth3 score.</param>
/// <param name="Accepted">Whether the score meets the supplied policy.</param>
/// <param name="PolicyIdentifier">The applied policy name and revision.</param>
public sealed record FingerprintComparisonResult(
    int RawScore,
    bool Accepted,
    string PolicyIdentifier
);
