// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Virtual camera parameters for approximate canonical-face geometry.</summary>
public sealed record PortraitPoseOptions
{
    /// <summary>Gets the virtual vertical field of view; Google's sample camera uses 63 degrees.</summary>
    public double VerticalFieldOfViewDegrees { get; init; } = 63;

    /// <summary>Gets the documented Google sample-camera policy, without asserting physical calibration.</summary>
    public static PortraitPoseOptions Default { get; } = new();

    internal bool IsValid =>
        double.IsFinite(VerticalFieldOfViewDegrees) && VerticalFieldOfViewDegrees is > 0 and < 180;
}
