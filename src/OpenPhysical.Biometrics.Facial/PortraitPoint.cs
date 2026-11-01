// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>A position in orientation-corrected source pixels, measured from the upper-left corner.</summary>
/// <param name="X">Horizontal position, increasing to the right.</param>
/// <param name="Y">Vertical position, increasing downwards.</param>
public readonly record struct PortraitPoint(double X, double Y);
