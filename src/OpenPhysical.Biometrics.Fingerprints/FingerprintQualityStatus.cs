// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>Describes the image condition reported by an NFIQ 1 assessment.</summary>
public enum FingerprintQualityStatus
{
    /// <summary>The classifier assessed the image normally.</summary>
    Assessed,

    /// <summary>The image contains no usable fingerprint foreground.</summary>
    EmptyImage,

    /// <summary>The image contains too few detected minutiae for normal classification.</summary>
    InsufficientMinutiae,
}
