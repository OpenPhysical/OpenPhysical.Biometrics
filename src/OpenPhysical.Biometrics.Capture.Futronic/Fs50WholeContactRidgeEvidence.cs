// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Full weak-component witness independent of photometric contact partitions.</summary>
/// <param name="ObservedContactDigest">SHA-256 of selected full weak mask W.</param>
/// <param name="ObservedArea">Included pixels in W.</param>
/// <param name="RidgeExteriorArea">Unsupported exterior pixels outside enclosed ridge continuation.</param>
/// <param name="LargestRidgeExteriorArea">Largest connected unsupported exterior region.</param>
public sealed record Fs50WholeContactRidgeEvidence(
    string ObservedContactDigest,
    int ObservedArea,
    int RidgeExteriorArea,
    int LargestRidgeExteriorArea
);
