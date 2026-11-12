// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;

internal static class TemplateProcessingAssociation
{
    internal static UnitResult<DesktopError> Check(
        TemplateProcessingValidated value,
        TemplateManifestImage image
    )
    {
        var policyDigest = value.Policy.Digest;
        var quality = TemplateNfiq1Metadata.Read(image.Quality);
        var unified = TemplateUnifiedQualityMetadata.Read(image.Nfiq2);
        var selected = Selected(value, image);
        if (
            value.Manifest.AlgorithmVersion
                != OpenPhysical.Biometrics.Capture.FingerprintPreprocessing.Version
            || !TemplatePngFiles.Same(policyDigest, value.Manifest.PolicyDigest)
            || value.Source.Sequence != image.FrameSequence
            || value.Source.CapturedAt != image.CapturedAtUtc
            || quality.IsFailure
            || unified.IsFailure
            || selected.Length != 1
            || !SameQuality(
                value.ProcessedAssessment,
                new CaptureImageQualityEvidence(
                    image.PixelSha256,
                    CaptureImageRole.Processed,
                    quality.Value,
                    unified.Value,
                    value.ProcessedAssessment.Diagnostics
                )
            )
            || !SameQuality(value.ProcessedAssessment, selected[0].Quality)
        )
            return TemplatePngFiles.Invalid(
                "Processing source, selected candidate, policy, or assessed quality does not match the primary image."
            );
        return UnitResult.Success<DesktopError>();
    }

    private static CaptureCandidateSummary[] Selected(
        TemplateProcessingValidated value,
        TemplateManifestImage image
    ) =>
        value
            .Candidates.Where(candidate =>
                TemplatePngFiles.Same(candidate.ProcessedDigest, image.PixelSha256)
                && TemplatePngFiles.Same(
                    candidate.OriginalDigest,
                    value.Manifest.Original.PixelSha256
                )
                && candidate.Sequence == image.FrameSequence
                && candidate.CapturedAt == image.CapturedAtUtc
            )
            .ToArray();

    private static bool SameQuality(
        CaptureImageQualityEvidence left,
        CaptureImageQualityEvidence right
    ) =>
        JsonSerializer.Serialize(TemplateQualityEvidenceMetadata.From(left))
        == JsonSerializer.Serialize(TemplateQualityEvidenceMetadata.From(right));
}
