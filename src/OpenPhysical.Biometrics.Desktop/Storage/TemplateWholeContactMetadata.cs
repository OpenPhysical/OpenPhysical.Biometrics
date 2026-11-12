// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record TemplateWholeContactMetadata(
    string Decision,
    Fs50WholeContactIdentity Identity,
    TemplateWholeContactPolicyMetadata Policy,
    string PolicyDigest,
    string Version,
    JsonElement ReferenceDigest,
    JsonElement ReferenceKeyDigest,
    JsonElement QualifiedAnchorDigest,
    Fs50WholeContactMetrics Metrics,
    Fs50WholeContactRidgeEvidence RidgeEvidence,
    Fs50WholeContactMetrics ObservedMetrics
)
{
    internal static JsonElement Write(Fs50WholeContactRecord record) =>
        JsonSerializer.SerializeToElement(
            new TemplateWholeContactMetadata(
                record.Decision.ToString(),
                record.Identity,
                TemplateWholeContactPolicyMetadata.From(record.Policy),
                record.PolicyDigest,
                record.Version,
                TemplateOptionalHashMetadata.Write(record.ReferenceDigest),
                TemplateOptionalHashMetadata.Write(record.ReferenceKeyDigest),
                TemplateOptionalHashMetadata.Write(record.QualifiedAnchorDigest),
                record.Metrics,
                record.RidgeEvidence,
                record.ObservedMetrics
            )
        );

    internal static Result<Fs50WholeContactRecord, DesktopError> Read(JsonElement value) =>
        DesktopEdge
            .Try(
                () => value.Deserialize<TemplateWholeContactMetadata>(),
                "Storage.Manifest",
                "Original qualification metadata could not be parsed."
            )
            .Bind(metadata =>
                metadata is not null
                    ? metadata.Parse()
                    : TemplatePngFiles.Invalid("Original qualification metadata is missing.")
            );

    private Result<Fs50WholeContactRecord, DesktopError> Parse()
    {
        if (
            Identity is null
            || Policy is null
            || Metrics is null
            || RidgeEvidence is null
            || ObservedMetrics is null
            || Version != Fs50WholeContactPolicy.Version
            || !Enum.TryParse<Fs50WholeContactDecision>(Decision, out var decision)
            || !Enum.IsDefined(decision)
            || Decision != decision.ToString()
        )
            return TemplatePngFiles.Invalid("Original qualification metadata is invalid.");
        return Policy
            .Read()
            .Ensure(
                policy => TemplatePngFiles.Same(policy.Digest, PolicyDigest),
                TemplatePngFiles.Invalid("Original qualification policy digest does not match.")
            )
            .Bind(policy => Record(policy, decision));
    }

    private Result<Fs50WholeContactRecord, DesktopError> Record(
        Fs50WholeContactPolicy policy,
        Fs50WholeContactDecision decision
    ) =>
        TemplateOptionalHashMetadata
            .Read(ReferenceDigest)
            .Bind(reference =>
                TemplateOptionalHashMetadata
                    .Read(ReferenceKeyDigest)
                    .Bind(key =>
                        TemplateOptionalHashMetadata
                            .Read(QualifiedAnchorDigest)
                            .Map(
                                (Maybe<string> anchor) =>
                                    new Fs50WholeContactRecord(
                                        decision,
                                        Identity,
                                        policy,
                                        reference,
                                        key,
                                        anchor,
                                        Metrics,
                                        RidgeEvidence,
                                        ObservedMetrics
                                    )
                            )
                    )
            );
}
