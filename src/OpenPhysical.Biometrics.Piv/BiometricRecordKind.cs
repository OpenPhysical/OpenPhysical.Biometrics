// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

/// <summary>Identifies the binary record interpreted by inspection.</summary>
public enum BiometricRecordKind
{
    /// <summary>The bytes have an unknown record format.</summary>
    Unknown,

    /// <summary>A raw INCITS 378-2004 fingerprint record.</summary>
    Fingerprints,

    /// <summary>A raw INCITS 385-2004 facial image record.</summary>
    FacialImages,

    /// <summary>A PIV CBEFF header, biometric record, and detached security block.</summary>
    Cbeff,
}
