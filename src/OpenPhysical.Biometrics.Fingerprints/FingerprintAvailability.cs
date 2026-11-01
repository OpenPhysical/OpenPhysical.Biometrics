// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>Describes whether a selected enrollment finger is usable.</summary>
public enum FingerprintAvailability
{
    /// <summary>A capture is available for extraction.</summary>
    Available,

    /// <summary>A temporary condition prevents use of this finger.</summary>
    TemporarilyUnusable,

    /// <summary>A permanent condition prevents use of this finger.</summary>
    PermanentlyUnusable,
}
