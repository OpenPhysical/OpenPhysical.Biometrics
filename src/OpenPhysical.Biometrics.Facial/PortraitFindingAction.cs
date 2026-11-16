// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>The operator action needed to resolve a portrait finding.</summary>
public enum PortraitFindingAction
{
    /// <summary>Review or adjust the captured image without another acquisition.</summary>
    Review,

    /// <summary>Acquire a new image; editing the crop cannot resolve the finding.</summary>
    Retake,
}
