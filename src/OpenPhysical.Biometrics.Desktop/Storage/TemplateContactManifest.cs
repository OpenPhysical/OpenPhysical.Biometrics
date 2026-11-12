// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record TemplateContactManifest(
    string SourceDigest,
    string PolicyDigest,
    string Version,
    Fs50EvidenceOptions Policy,
    int CandidateArea,
    int RidgeSupportArea,
    int AcceptedArea,
    int RejectedArea,
    int ComponentCount,
    string Issue,
    TemplateContactStatistics AcceptedStatistics,
    TemplateContactStatistics ObservedStatistics,
    ImmutableArray<TemplateContactComponent> Components
)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Planes { get; init; }
}
