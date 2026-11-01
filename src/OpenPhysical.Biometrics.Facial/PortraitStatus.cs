// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Expected outcomes of portrait preparation.</summary>
public enum PortraitStatus
{
    /// <summary>The source supports the selected portrait geometry.</summary>
    Prepared,

    /// <summary>No face passed detection and landmark presence checks.</summary>
    NoFace,

    /// <summary>The caller must select one of several faces.</summary>
    FaceSelectionRequired,

    /// <summary>Geometry requires reviewed measurements or a new capture.</summary>
    ReviewRequired,
}
