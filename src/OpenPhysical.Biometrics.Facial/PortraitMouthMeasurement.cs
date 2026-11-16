// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Inner-lip gaps measured perpendicular to, and normalized by, inner mouth width.</summary>
/// <param name="CenterOpeningRatio">Gap at the center inner lips.</param>
/// <param name="ViewerLeftOpeningRatio">Gap on the viewer's left side of the inner lips.</param>
/// <param name="ViewerRightOpeningRatio">Gap on the viewer's right side of the inner lips.</param>
public sealed record PortraitMouthMeasurement(
    double CenterOpeningRatio,
    double ViewerLeftOpeningRatio,
    double ViewerRightOpeningRatio
)
{
    /// <summary>Gets the largest measured inner-lip gap relative to mouth width.</summary>
    public double MaximumOpeningRatio =>
        Math.Max(CenterOpeningRatio, Math.Max(ViewerLeftOpeningRatio, ViewerRightOpeningRatio));
}
