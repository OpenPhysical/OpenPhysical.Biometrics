// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record TemplateQualificationSource(
    FingerprintProcessingSource Source,
    string DriverVersion,
    CaptureGeometry Geometry
)
{
    internal static TemplateQualificationSource From(
        CaptureFrame frame,
        FingerprintProcessingSource source
    ) => new(source, frame.Provenance.DriverVersion, frame.Provenance.OutputGeometry);

    internal static Result<TemplateQualificationSource, DesktopError> Read(
        TemplateManifestImage image,
        FingerprintProcessingSource source
    )
    {
        if (
            !image.Provenance.TryGetProperty("DriverVersion", out var driver)
            || driver.ValueKind != JsonValueKind.String
            || driver.GetString() is not { } version
            || string.IsNullOrWhiteSpace(version)
        )
            return TemplatePngFiles.Invalid("Original acquisition driver identity is missing.");
        return CaptureGeometry
            .Create(image.Width, image.Height)
            .MapError(error => TemplatePngFiles.Invalid(error.Message))
            .Map(geometry => new TemplateQualificationSource(source, version, geometry));
    }

    internal UnitResult<DesktopError> Check(Fs50WholeContactRecord record) =>
        record
            .ValidateSource(Source, DriverVersion, Geometry)
            .MapError(error => TemplatePngFiles.Invalid(error.Message));
}
