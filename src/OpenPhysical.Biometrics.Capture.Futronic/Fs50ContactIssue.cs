// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Explains why observed texture is not eligible fingerprint contact.</summary>
public enum Fs50ContactIssue
{
    /// <summary>No rejected contact was observed.</summary>
    None,

    /// <summary>The foreground is too faint for a usable acquisition.</summary>
    TooLight,

    /// <summary>Texture lacks sufficient coherent, alternating ridge evidence.</summary>
    InsufficientRidgeEvidence,
}
