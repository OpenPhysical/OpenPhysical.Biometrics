// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Piv;

/// <summary>Numeric framing checks, not a complete PIV portrait or optical-conformance decision.</summary>
/// <param name="ImageDimensionsSatisfied">Whether the encoded image dimensions satisfy the profile.</param>
/// <param name="HeadResolutionSatisfied">Whether head width satisfies the conservative preparation policy.</param>
/// <param name="HeadWidthRatioSatisfied">Whether image-to-head width meets the minimum ratio.</param>
/// <param name="EyePositionSatisfied">Whether eyes lie in the permitted vertical band.</param>
/// <param name="HeadHeightSatisfied">Whether crown-to-chin extent is within the maximum.</param>
public sealed record PivPortraitGeometryAssessment(
    bool ImageDimensionsSatisfied,
    bool HeadResolutionSatisfied,
    bool HeadWidthRatioSatisfied,
    bool EyePositionSatisfied,
    bool HeadHeightSatisfied
)
{
    /// <summary>Gets whether all supplied numeric measurements satisfy these checks.</summary>
    public bool IsSatisfied =>
        ImageDimensionsSatisfied
        && HeadResolutionSatisfied
        && HeadWidthRatioSatisfied
        && EyePositionSatisfied
        && HeadHeightSatisfied;
}
