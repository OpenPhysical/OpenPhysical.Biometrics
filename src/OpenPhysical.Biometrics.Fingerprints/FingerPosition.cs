// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

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
