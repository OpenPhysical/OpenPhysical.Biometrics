// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal sealed record TemplateProcessingPolicyMetadata(
    bool ClaheEnabled,
    int TileSize,
    double ClipLimit,
    double Blend,
    int Brightness,
    int Contrast
)
{
    internal static TemplateProcessingPolicyMetadata From(FingerprintPreprocessingPolicy value) =>
        new(
            value.ClaheEnabled,
            value.TileSize,
            value.ClipLimit,
            value.Blend,
            value.Brightness,
            value.Contrast
        );

    internal Result<FingerprintPreprocessingPolicy, DesktopError> Read() =>
        FingerprintPreprocessingPolicy
            .Create(ClaheEnabled, TileSize, ClipLimit, Blend, Brightness, Contrast)
            .MapError(error => TemplatePngFiles.Invalid(error.Message));
}
