// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal static class TemplateManifestCompanions
{
    internal static UnitResult<DesktopError> Check(
        TemplateManifest manifest,
        FingerprintRecord record
    )
    {
        var names = manifest.Images.Select(image => image.FileName).ToList();
        foreach (var image in manifest.Images)
        {
            var parsed = TemplateProcessingValidation.Read(image.Processing, image);
            if (parsed.IsFailure)
                return parsed.Error;
            if (
                parsed.Value.ProcessedAssessment.Nfiq1.PivQuality
                != record.Views[image.ViewIndex].Quality
            )
                return TemplatePngFiles.Invalid(
                    "The processing assessment does not match the quality in the template view."
                );
            var added = Add(names, parsed.Value.Manifest, image.Width * image.Height);
            if (added.IsFailure)
                return added.Error;
            var spatial = AddSpatial(names, parsed.Value, image);
            if (spatial.IsFailure)
                return spatial.Error;
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Count
            ? UnitResult.Success<DesktopError>()
            : TemplatePngFiles.Invalid("Each paired PNG must have a distinct companion filename.");
    }

    private static UnitResult<DesktopError> AddSpatial(
        List<string> names,
        TemplateProcessingValidated processing,
        TemplateManifestImage image
    )
    {
        var hull = TemplateContourValidation.Read(processing, image);
        if (hull.IsFailure)
            return hull.Error;
        names.AddRange(hull.Value.Manifest.Planes.Additional.Select(plane => plane.FileName));
        return UnitResult.Success<DesktopError>();
    }

    private static UnitResult<DesktopError> Add(
        List<string> names,
        TemplateProcessingManifest value,
        int pixels
    )
    {
        names.AddRange([
            value.Original.FileName,
            value.Masks.Retained.FileName,
            value.Masks.Inner.FileName,
        ]);
        if (value.ContactDiagnostics.ValueKind == JsonValueKind.Undefined)
            return UnitResult.Success<DesktopError>();
        var contact = TemplateContactReader.Parse(value.ContactDiagnostics, pixels);
        if (contact.IsFailure)
            return contact.Error;
        if (contact.Value.Planes.ValueKind == JsonValueKind.Undefined)
            return UnitResult.Success<DesktopError>();
        var planes = TemplateContactReader.Planes(contact.Value.Planes);
        if (planes.IsFailure)
            return planes.Error;
        var descriptors = TemplateContactPlaneFiles.All(planes.Value);
        if (descriptors.IsFailure)
            return descriptors.Error;
        names.AddRange(descriptors.Value.Select(plane => plane.FileName));
        return UnitResult.Success<DesktopError>();
    }
}
