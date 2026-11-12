// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal static class TemplateProvenance
{
    internal static JsonElement For(CaptureFrame frame)
    {
        var provenance = frame.Provenance;
        var device = provenance.Device;
        return JsonSerializer.SerializeToElement(
            new
            {
                Device = new
                {
                    device.DeviceId,
                    device.DriverId,
                    device.Model,
                    Serial = device.Serial.Match(value => value, () => string.Empty),
                    device.Capabilities,
                },
                provenance.DriverVersion,
                provenance.RawGeometry,
                provenance.OutputGeometry,
                provenance.Settings,
                CalibrationSha256 = Convert.ToHexString(provenance.CalibrationDigest.AsSpan()),
                provenance.CorrectionVersion,
                ResolutionKind = provenance.Resolution.Kind.ToString(),
                HorizontalPpi = provenance.Resolution.HorizontalPpi.Match(
                    value => new[] { value },
                    () => Array.Empty<double>()
                ),
                VerticalPpi = provenance.Resolution.VerticalPpi.Match(
                    value => new[] { value },
                    () => Array.Empty<double>()
                ),
                provenance.Resolution.EvidenceReference,
                provenance.Qualification,
                Stage = frame.Stage.ToString(),
                frame.PixelFormat,
            }
        );
    }
}
