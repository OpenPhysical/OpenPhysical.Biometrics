// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record TemplateQualityEvidenceMetadata(
    string ImageDigest,
    string Role,
    TemplateNfiq1Metadata Nfiq1,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] JsonElement Nfiq2,
    ImmutableArray<DesktopError> Diagnostics
)
{
    internal static TemplateQualityEvidenceMetadata From(CaptureImageQualityEvidence value) =>
        new(
            value.ImageDigest.ToLowerInvariant(),
            value.Role.ToString(),
            TemplateNfiq1Metadata.From(value.Nfiq1),
            TemplateUnifiedQualityMetadata.Write(value.Nfiq2),
            value.Diagnostics
        );

    internal Result<CaptureImageQualityEvidence, DesktopError> Read(
        CaptureImageRole role,
        string digest
    )
    {
        if (
            !TemplatePngFiles.Same(ImageDigest, digest)
            || Role != role.ToString()
            || Nfiq1 is null
            || Diagnostics.IsDefault
            || Diagnostics.Any(error =>
                error is null
                || string.IsNullOrWhiteSpace(error.Code)
                || string.IsNullOrWhiteSpace(error.Message)
            )
        )
            return TemplatePngFiles.Invalid(
                "A quality assessment is not associated with its declared image role and hash."
            );
        return Nfiq1
            .ToAssessment()
            .Bind(quality =>
                TemplateUnifiedQualityMetadata
                    .Read(Nfiq2)
                    .Map(
                        (Maybe<FingerprintUnifiedQualityAssessment> unified) =>
                            new CaptureImageQualityEvidence(
                                digest,
                                role,
                                quality,
                                unified,
                                Diagnostics
                            )
                    )
            );
    }
}
