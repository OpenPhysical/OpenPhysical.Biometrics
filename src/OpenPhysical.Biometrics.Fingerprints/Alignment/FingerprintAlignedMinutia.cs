// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>Contains a transformed reference point for visualization in the original probe image.</summary>
/// <param name="X">Floating-point horizontal probe pixel coordinate.</param>
/// <param name="Y">Floating-point vertical probe pixel coordinate.</param>
/// <param name="AngleDegrees">Counterclockwise direction from positive X, in degrees.</param>
/// <param name="Type">Original reference minutia type.</param>
/// <param name="Quality">Original reference minutia quality.</param>
/// <param name="Matched">Whether this reference point has a supported probe correspondence.</param>
public sealed record FingerprintAlignedMinutia(
    double X,
    double Y,
    double AngleDegrees,
    byte Type,
    byte Quality,
    bool Matched
);
