// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal sealed record TemplateProcessingSourceMetadata(
    string DeviceId,
    long Sequence,
    DateTimeOffset CapturedAt,
    int Exposure,
    string CorrectionVersion,
    string CalibrationDigest
)
{
    internal static TemplateProcessingSourceMetadata From(FingerprintProcessingSource value) =>
        new(
            value.DeviceId,
            value.Sequence,
            value.CapturedAt,
            value.Exposure,
            value.CorrectionVersion,
            value.CalibrationDigest
        );

    internal Result<FingerprintProcessingSource, DesktopError> Read() =>
        FingerprintProcessingSource
            .Create(DeviceId, Sequence, CapturedAt, Exposure, CorrectionVersion, CalibrationDigest)
            .MapError(error => TemplatePngFiles.Invalid(error.Message));
}
