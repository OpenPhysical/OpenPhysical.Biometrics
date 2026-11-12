// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record TemplateSelectionPolicyMetadata(int MinimumNfiq1Gain, int MinimumNfiq2Gain)
{
    internal static TemplateSelectionPolicyMetadata From(FingerprintQualitySelectionPolicy value) =>
        new(value.MinimumNfiq1Gain, value.MinimumNfiq2Gain);

    internal Result<FingerprintQualitySelectionPolicy, DesktopError> Read() =>
        FingerprintQualitySelectionPolicy
            .Create(MinimumNfiq1Gain, MinimumNfiq2Gain)
            .MapError(error => TemplatePngFiles.Invalid(error.Message));
}
