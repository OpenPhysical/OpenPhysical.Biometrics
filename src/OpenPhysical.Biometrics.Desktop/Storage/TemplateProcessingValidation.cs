// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;

internal static class TemplateProcessingValidation
{
    internal static Result<TemplateProcessingValidated, DesktopError> Read(
        JsonElement element,
        TemplateManifestImage image
    )
    {
        if (element.ValueKind != JsonValueKind.Object)
            return Invalid("Advertised processing evidence must be an object.");
        return DesktopEdge
            .Try(
                () => element.Deserialize<TemplateProcessingManifest>(),
                "Storage.Manifest",
                "Processing evidence could not be parsed."
            )
            .Bind(value =>
                value is not null ? Check(value, image) : Invalid("Processing evidence is missing.")
            );
    }

    internal static string SettingsHash(TemplateProcessingManifest value) =>
        TemplateSha256.Of(
            JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    value.AlgorithmVersion,
                    value.Policy,
                    value.SelectionPolicy,
                    value.InputRole,
                    OriginalQualification = Canonical(value.OriginalQualification),
                    OriginalAcquisitionPolicy = Canonical(value.OriginalAcquisitionPolicy),
                    ContactDiagnostics = Canonical(value.ContactDiagnostics),
                    Contour = Canonical(value.Contour),
                }
            )
        );

    private static string Canonical(JsonElement value) =>
        value.ValueKind is JsonValueKind.Object or JsonValueKind.Null
            ? JsonSerializer.Serialize(value)
            : string.Empty;

    internal static Result<TemplateProcessingValidated, DesktopError> Check(
        TemplateProcessingManifest value,
        TemplateManifestImage image
    )
    {
        if (!Shape(value, image))
            return Invalid(
                "Processing companions, version, settings hash, or selection evidence are invalid."
            );
        return value
            .Policy.Read()
            .Bind(policy =>
                value
                    .SelectionPolicy.Read()
                    .Bind(selection =>
                        value
                            .Source.Read()
                            .Bind(source => Assessments(value, image, policy, selection, source))
                    )
            )
            .Check(result => TemplateProcessingAssociation.Check(result, image))
            .Check(result => TemplateProcessingSourceAssociation.Check(result, image))
            .Bind(Acquisition);
    }

    private static Result<TemplateProcessingValidated, DesktopError> Acquisition(
        TemplateProcessingValidated value
    ) =>
        CaptureAcquisitionPolicy
            .Read(value.Manifest.OriginalAcquisitionPolicy)
            .MapError(error => TemplatePngFiles.Invalid(error.Message))
            .Map(policy => value with { OriginalAcquisitionPolicy = policy });

    private static Result<TemplateProcessingValidated, DesktopError> Assessments(
        TemplateProcessingManifest value,
        TemplateManifestImage image,
        OpenPhysical.Biometrics.Capture.FingerprintPreprocessingPolicy policy,
        OpenPhysical.Biometrics.Fingerprints.FingerprintQualitySelectionPolicy selection,
        OpenPhysical.Biometrics.Capture.FingerprintProcessingSource source
    ) =>
        value
            .OriginalAssessment.Read(CaptureImageRole.Original, value.Original.PixelSha256)
            .Bind(original =>
                value
                    .ProcessedAssessment.Read(CaptureImageRole.Processed, image.PixelSha256)
                    .Bind(processed =>
                        Candidates(value, image)
                            .Map(candidates => new TemplateProcessingValidated(
                                value,
                                policy,
                                selection,
                                source,
                                original,
                                processed,
                                Enum.Parse<CaptureSelectionReason>(value.SelectionReason),
                                candidates
                            ))
                    )
            );

    private static bool Shape(TemplateProcessingManifest value, TemplateManifestImage image) =>
        value.Version == 1
        && value.Policy is not null
        && value.SelectionPolicy is not null
        && value.Source is not null
        && value.OriginalAssessment is not null
        && value.ProcessedAssessment is not null
        && value.Masks is not null
        && TemplatePngFiles.Valid(value.Original)
        && TemplatePngFiles.Valid(value.Masks.Retained)
        && TemplatePngFiles.Valid(value.Masks.Inner)
        && new[]
        {
            image.FileName,
            value.Original.FileName,
            value.Masks.Retained.FileName,
            value.Masks.Inner.FileName,
        }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == 4
        && TemplatePngFiles.Hash(value.Masks.Digest)
        && TemplatePngFiles.Hash(value.PolicyDigest)
        && TemplatePngFiles.Hash(value.Identity)
        && value.ProcessingRevision >= 0
        && !string.IsNullOrWhiteSpace(value.AlgorithmVersion)
        && TemplatePngFiles.Same(value.SettingsSha256, SettingsHash(value))
        && value.OriginalAcquisitionPolicy.ValueKind == JsonValueKind.Object
        && value.Contour.ValueKind == JsonValueKind.Object
        && value.ContactDiagnostics.ValueKind == JsonValueKind.Object
        && !string.IsNullOrWhiteSpace(value.InputRole)
        && value.OriginalQualification.ValueKind is JsonValueKind.Object or JsonValueKind.Null
        && Enum.TryParse<CaptureSelectionReason>(value.SelectionReason, out var reason)
        && Enum.IsDefined(reason)
        && !value.Candidates.IsDefaultOrEmpty
        && value.Candidates.Length <= 5
        && value.Candidates.All(candidate => candidate is not null)
        && (reason != CaptureSelectionReason.Manual || value.Candidates.Length == 1);

    private static Result<ImmutableArray<CaptureCandidateSummary>, DesktopError> Candidates(
        TemplateProcessingManifest value,
        TemplateManifestImage image
    )
    {
        var result = ImmutableArray.CreateBuilder<CaptureCandidateSummary>();
        foreach (var candidate in value.Candidates)
        {
            var read = candidate.Read(image.Width * image.Height);
            if (read.IsFailure)
                return read.Error;
            result.Add(read.Value);
        }
        return result.ToImmutable();
    }

    private static DesktopError Invalid(string message) => TemplatePngFiles.Invalid(message);
}
