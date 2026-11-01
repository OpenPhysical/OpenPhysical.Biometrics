// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>Describes the acquisition method accepted for a PIV minutiae record.</summary>
public enum FingerprintImpression : byte
{
    /// <summary>A plain image acquired from a live finger.</summary>
    PlainLiveScan = 0,

    /// <summary>A plain image acquired from a non-live source.</summary>
    PlainNonLiveScan = 2,
}
