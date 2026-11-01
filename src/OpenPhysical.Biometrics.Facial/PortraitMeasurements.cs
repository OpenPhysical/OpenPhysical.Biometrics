// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Measurements supporting the selected crop.</summary>
/// <param name="HeadWidth">Ear-attachment separation in output pixels.</param>
/// <param name="HeadHeight">Crown-to-chin extent in output pixels.</param>
/// <param name="EyeHeightFraction">Eye position measured down from the output top.</param>
/// <param name="ReviewedHeadBoundary">Whether crown and both ear attachments were supplied by an operator.</param>
public sealed record PortraitMeasurements(
    double HeadWidth,
    double HeadHeight,
    double EyeHeightFraction,
    bool ReviewedHeadBoundary
)
{
    /// <summary>Gets how the crown boundary was established.</summary>
    public PortraitMeasurementOrigin CrownOrigin { get; init; } =
        PortraitMeasurementOrigin.Estimated;

    /// <summary>Gets how the left ear attachment was established.</summary>
    public PortraitMeasurementOrigin ViewerLeftEarOrigin { get; init; } =
        PortraitMeasurementOrigin.Estimated;

    /// <summary>Gets how the right ear attachment was established.</summary>
    public PortraitMeasurementOrigin ViewerRightEarOrigin { get; init; } =
        PortraitMeasurementOrigin.Estimated;

    /// <summary>Gets how the chin boundary was established.</summary>
    public PortraitMeasurementOrigin ChinOrigin { get; init; } = PortraitMeasurementOrigin.Mesh;
}
