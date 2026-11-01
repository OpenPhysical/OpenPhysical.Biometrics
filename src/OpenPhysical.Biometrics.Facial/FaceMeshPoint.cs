// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>A MediaPipe landmark in source-pixel units.</summary>
/// <param name="X">Horizontal position.</param>
/// <param name="Y">Vertical position.</param>
/// <param name="Z">Relative depth, meaningful only within the detected face.</param>
public readonly record struct FaceMeshPoint(double X, double Y, double Z);
