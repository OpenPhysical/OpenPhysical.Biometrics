// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal static class TemplateFeatureAdmission
{
    internal static UnitResult<DesktopError> Check(
        FingerprintView view,
        FingerprintProcessingMask mask
    ) =>
        view.Minutiae.All(point =>
            point.X < mask.Width
            && point.Y < mask.Height
            && mask.Inner[point.Y * mask.Width + point.X] != 0
        )
            ? UnitResult.Success<DesktopError>()
            : TemplatePngFiles.Invalid(
                "A template point is outside the saved eligible feature region."
            );

    internal static UnitResult<DesktopError> Check(
        FingerprintRecord record,
        ImmutableArray<TemplateImage> images
    )
    {
        foreach (var image in images)
        {
            if (image.Processing.HasNoValue)
                return TemplatePngFiles.Invalid(
                    "The Studio feature admission evidence is missing."
                );
            var result = Check(record.Views[image.ViewIndex], image.Processing.Value.Mask);
            if (result.IsFailure)
                return result.Error;
        }
        return UnitResult.Success<DesktopError>();
    }
}
