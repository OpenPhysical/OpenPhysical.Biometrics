// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record TemplateProcessingManifest(
    int Version,
    TemplatePngDescriptor Original,
    TemplateProcessingMasks Masks,
    TemplateQualityEvidenceMetadata OriginalAssessment,
    TemplateQualityEvidenceMetadata ProcessedAssessment,
    TemplateProcessingPolicyMetadata Policy,
    TemplateSelectionPolicyMetadata SelectionPolicy,
    TemplateProcessingSourceMetadata Source,
    string AlgorithmVersion,
    long ProcessingRevision,
    string PolicyDigest,
    string SettingsSha256,
    string Identity,
    string SelectionReason,
    ImmutableArray<TemplateCandidateMetadata> Candidates
)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement ContactDiagnostics { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement OriginalAcquisitionPolicy { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Contour { get; init; }

    public string InputRole { get; init; } = string.Empty;

    public JsonElement OriginalQualification { get; init; }
}
