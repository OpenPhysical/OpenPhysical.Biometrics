// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal static class TemplateProcessingSourceAssociation
{
    internal static UnitResult<DesktopError> Check(
        TemplateProcessingValidated value,
        TemplateManifestImage image
    )
    {
        var result = DesktopEdge.Try(
            () => Matches(value.Source, image.Provenance),
            "Storage.Manifest",
            "Processing acquisition provenance is incomplete or invalid."
        );
        return result.IsFailure ? result.Error
            : result.Value ? UnitResult.Success<DesktopError>()
            : TemplatePngFiles.Invalid(
                "Processing source identity contradicts the declared acquisition provenance."
            );
    }

    internal static bool Matches(CapturedFingerprint capture)
    {
        var frame = capture.Frame;
        var provenance = frame.Provenance;
        var expected = FingerprintProcessingSource.Create(
            provenance.Device.DeviceId,
            frame.Sequence,
            frame.CapturedAt,
            provenance.Settings.Exposure,
            provenance.CorrectionVersion,
            Convert.ToHexString(provenance.CalibrationDigest.AsSpan())
        );
        return expected.IsSuccess && expected.Value == capture.Processing.Value.Artifact.Source;
    }

    private static bool Matches(FingerprintProcessingSource source, JsonElement provenance) =>
        provenance.GetProperty("Device").GetProperty("DeviceId").GetString() == source.DeviceId
        && provenance.GetProperty("Settings").GetProperty("Exposure").GetInt32() == source.Exposure
        && provenance.GetProperty("CorrectionVersion").GetString() == source.CorrectionVersion
        && string.Equals(
            provenance.GetProperty("CalibrationSha256").GetString(),
            source.CalibrationDigest,
            StringComparison.OrdinalIgnoreCase
        );
}
