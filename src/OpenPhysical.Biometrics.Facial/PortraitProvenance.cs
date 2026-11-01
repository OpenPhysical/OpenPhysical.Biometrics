// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Identifies the model artifacts and geometry policy used for preparation.</summary>
public static class PortraitProvenance
{
    /// <summary>The immutable source revision of the published ONNX conversion.</summary>
    public const string ModelRevision = "3bdf1e0d100a4c709766c051a0c9e9cdab1fed62";

    /// <summary>The versioned application crop policy implementing Full Frontal geometry.</summary>
    public const string GeometryPolicy = "piv-full-frontal-480x640-v1";
}
