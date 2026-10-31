// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>Identifies a single finger using the ANSI finger-position codes.</summary>
public enum FingerPosition : byte
{
    /// <summary>The right thumb.</summary>
    RightThumb = 1,

    /// <summary>The right index finger.</summary>
    RightIndex = 2,

    /// <summary>The right middle finger.</summary>
    RightMiddle = 3,

    /// <summary>The right ring finger.</summary>
    RightRing = 4,

    /// <summary>The right little finger.</summary>
    RightLittle = 5,

    /// <summary>The left thumb.</summary>
    LeftThumb = 6,

    /// <summary>The left index finger.</summary>
    LeftIndex = 7,

    /// <summary>The left middle finger.</summary>
    LeftMiddle = 8,

    /// <summary>The left ring finger.</summary>
    LeftRing = 9,

    /// <summary>The left little finger.</summary>
    LeftLittle = 10,
}

/// <summary>Describes the acquisition method accepted for a PIV minutiae record.</summary>
public enum FingerprintImpression : byte
{
    /// <summary>A plain image acquired from a live finger.</summary>
    PlainLiveScan = 0,

    /// <summary>A plain image acquired from a non-live source.</summary>
    PlainNonLiveScan = 2,
}

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
