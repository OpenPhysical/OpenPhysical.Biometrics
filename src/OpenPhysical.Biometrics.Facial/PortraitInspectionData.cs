// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Piv;

internal static class PortraitInspectionData
{
    internal static bool IsUsable(PortraitPreparationResult portrait) =>
        !portrait.RgbPixels.IsDefault
        && !portrait.Findings.IsDefault
        && portrait.RgbPixels.Length == PortraitOutput.Width * PortraitOutput.Height * 3
        && portrait.Transform.Match(
            value =>
                value.IsInvertible
                && double.IsFinite(value.TranslateX)
                && double.IsFinite(value.TranslateY),
            () => false
        )
        && portrait.Measurements.Match(ValidMeasurements, () => false);

    private static bool ValidMeasurements(PortraitMeasurements measurements) =>
        PivPortraitGeometry
            .Assess(
                PortraitOutput.Width,
                PortraitOutput.Height,
                measurements.HeadWidth,
                measurements.HeadHeight,
                measurements.EyeHeightFraction
            )
            .Match(value => value.IsSatisfied, _ => false);
}
