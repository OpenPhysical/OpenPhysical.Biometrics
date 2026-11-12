// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;

internal static class TemplateProcessingWriter
{
    internal static Result<JsonElement, DesktopError> Add(
        ImmutableArray<TemplatePackageFile>.Builder files,
        CapturedFingerprint capture,
        TemplateManifestImage image,
        TemplateSaveOptions options
    )
    {
        if (capture.Processing.HasNoValue)
            return TemplatePngFiles.Invalid(
                "A Studio capture package requires processing evidence."
            );
        var evidence = capture.Processing.Value;
        if (evidence.OriginalAcquisitionPolicy.HasNoValue)
            return TemplatePngFiles.Invalid(
                "A Studio package requires its complete acquisition policy snapshot."
            );
        if (!SourceMatches(capture))
            return TemplatePngFiles.Invalid(
                "Processing evidence belongs to a different original acquisition."
            );
        var stem = Path.GetFileNameWithoutExtension(image.FileName);
        return TemplatePngFiles
            .Add(files, stem + "-original.png", capture.Frame.Pixels, image.Width, image.Height)
            .Bind(original =>
                Masks(files, stem, evidence).Bind(masks => Manifest(capture, original, masks))
            )
            .Bind(manifest =>
                TemplateContactWriter
                    .Add(files, stem, evidence, options)
                    .Map(contact => manifest with { ContactDiagnostics = contact })
            )
            .Bind(manifest => Spatial(files, stem, evidence, manifest))
            .Map(manifest =>
                manifest with
                {
                    SettingsSha256 = TemplateProcessingValidation.SettingsHash(manifest),
                }
            )
            .Bind(manifest => Validate(manifest, image));
    }

    private static Result<JsonElement, DesktopError> Validate(
        TemplateProcessingManifest manifest,
        TemplateManifestImage image
    ) =>
        TemplateProcessingValidation
            .Check(manifest, image)
            .Check(value => TemplateContourValidation.Required(value, image))
            .Map(_ => JsonSerializer.SerializeToElement(manifest));

    private static Result<TemplateProcessingManifest, DesktopError> Spatial(
        ImmutableArray<TemplatePackageFile>.Builder files,
        string stem,
        CaptureProcessingEvidence evidence,
        TemplateProcessingManifest manifest
    ) =>
        TemplateContourWriter
            .Add(files, stem, evidence, manifest.Masks)
            .Map(contour => manifest with { Contour = contour });

    private static bool SourceMatches(CapturedFingerprint capture)
    {
        var artifact = capture.Processing.Value.Artifact;
        return TemplatePngFiles.Same(
                TemplateSha256.Of(capture.Frame.Pixels.ToArray()),
                artifact.OriginalDigest
            )
            && capture.Frame.Sequence == artifact.Source.Sequence
            && capture.Frame.CapturedAt == artifact.Source.CapturedAt
            && TemplateProcessingSourceAssociation.Matches(capture);
    }

    private static Result<TemplateProcessingMasks, DesktopError> Masks(
        ImmutableArray<TemplatePackageFile>.Builder files,
        string stem,
        CaptureProcessingEvidence evidence
    )
    {
        var artifact = evidence.Artifact;
        return TemplatePngFiles
            .Add(
                files,
                stem + "-retained.png",
                artifact.Mask.Retained,
                artifact.Width,
                artifact.Height
            )
            .Bind(retained =>
                TemplatePngFiles
                    .Add(
                        files,
                        stem + "-inner.png",
                        artifact.Mask.Inner,
                        artifact.Width,
                        artifact.Height
                    )
                    .Map(inner => new TemplateProcessingMasks(
                        retained,
                        inner,
                        artifact.Mask.Digest
                    ))
            );
    }

    private static Result<TemplateProcessingManifest, DesktopError> Manifest(
        CapturedFingerprint capture,
        TemplatePngDescriptor original,
        TemplateProcessingMasks masks
    )
    {
        var evidence = capture.Processing.Value;
        var artifact = evidence.Artifact;
        var selected = evidence
            .Candidates.Where(candidate =>
                TemplatePngFiles.Same(candidate.ProcessedDigest, artifact.ProcessedDigest)
                && candidate.Sequence == capture.Frame.Sequence
            )
            .ToArray();
        if (selected.Length != 1)
            return TemplatePngFiles.Invalid(
                "Processing evidence must identify one selected burst candidate."
            );
        var processed = selected[0].Quality;
        return TemplateOriginalQualification
            .Write(capture)
            .Map(qualification =>
                Create(capture, original, masks, processed) with
                {
                    OriginalQualification = qualification,
                }
            );
    }

    private static TemplateProcessingManifest Create(
        CapturedFingerprint capture,
        TemplatePngDescriptor original,
        TemplateProcessingMasks masks,
        CaptureImageQualityEvidence processed
    )
    {
        var evidence = capture.Processing.Value;
        var artifact = evidence.Artifact;
        var manifest = new TemplateProcessingManifest(
            1,
            original,
            masks,
            TemplateQualityEvidenceMetadata.From(evidence.OriginalAssessment),
            TemplateQualityEvidenceMetadata.From(processed),
            TemplateProcessingPolicyMetadata.From(artifact.Policy),
            TemplateSelectionPolicyMetadata.From(evidence.SelectionPolicy),
            TemplateProcessingSourceMetadata.From(artifact.Source),
            artifact.Version,
            artifact.ProcessingRevision,
            artifact.PolicyDigest,
            string.Empty,
            artifact.Identity,
            evidence.SelectionReason.ToString(),
            evidence.Candidates.Select(TemplateCandidateMetadata.From).ToImmutableArray()
        );
        return manifest with
        {
            InputRole = evidence.InputRole.ToString(),
            OriginalAcquisitionPolicy = evidence.OriginalAcquisitionPolicy.Match(
                value => value.ToJson(),
                () => default
            ),
            SettingsSha256 = TemplateProcessingValidation.SettingsHash(manifest),
        };
    }
}
