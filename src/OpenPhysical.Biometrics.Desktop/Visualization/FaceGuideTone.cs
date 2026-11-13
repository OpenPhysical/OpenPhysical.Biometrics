// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Meaning of a non-destructive facial inspection annotation.</summary>
public enum FaceGuideTone
{
    /// <summary>Portrait framing and eye-alignment diagram.</summary>
    Framing,

    /// <summary>A proportional suggested region, without a validity decision.</summary>
    Region,

    /// <summary>Google face-detection evidence, not a conformance decision.</summary>
    Detection,
}
