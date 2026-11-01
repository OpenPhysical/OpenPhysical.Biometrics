// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Distinguishes operator review from automatic anatomical measurements.</summary>
public enum PortraitMeasurementOrigin
{
    /// <summary>An automatic estimate of a head boundary.</summary>
    Estimated,

    /// <summary>A position inferred from the canonical mesh.</summary>
    Mesh,

    /// <summary>A coordinate supplied in the reviewed geometry file.</summary>
    Reviewed,
}
