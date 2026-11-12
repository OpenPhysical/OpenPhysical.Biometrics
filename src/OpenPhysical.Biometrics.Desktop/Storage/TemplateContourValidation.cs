// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class TemplateContourValidation
{
    internal static UnitResult<DesktopError> Required(
        TemplateProcessingValidated processing,
        TemplateManifestImage image
    )
    {
        var result = Read(processing, image);
        return result.IsFailure ? result.Error : UnitResult.Success<DesktopError>();
    }

    internal static Result<TemplateContourValidated, DesktopError> Read(
        TemplateProcessingValidated processing,
        TemplateManifestImage image
    )
    {
        if (
            image.Width != 800
            || image.Height != 750
            || processing.Manifest.Contour.ValueKind != JsonValueKind.Object
        )
            return Invalid();
        return DesktopEdge
            .Try(
                () => processing.Manifest.Contour.Deserialize<TemplateContourManifest>(),
                "Storage.Manifest",
                "Contact contour metadata could not be parsed."
            )
            .Bind(value =>
                value is not null && Shape(value, processing)
                    ? Validate(value, processing)
                    : Invalid()
            );
    }

    private static Result<TemplateContourValidated, DesktopError> Validate(
        TemplateContourManifest value,
        TemplateProcessingValidated processing
    ) =>
        value
            .Policy.Read()
            .Ensure(
                policy =>
                    value.AlgorithmVersion == Fs50ContactContourPolicy.Version
                    && TemplatePngFiles.Same(policy.Digest, value.PolicyDigest),
                Invalid()
            )
            .Bind(policy =>
                TemplateContactReader
                    .Parse(processing.Manifest.ContactDiagnostics, 600000)
                    .Check(contact => TemplateContactValidation.Metadata(contact, processing))
                    .Ensure(contact => ContactMatches(value, contact), Invalid())
                    .Map(contact => new TemplateContourValidated(value, policy, contact))
            );

    private static bool ContactMatches(
        TemplateContourManifest value,
        TemplateContactManifest contact
    ) =>
        TemplatePngFiles.Same(value.SourceDigest, contact.SourceDigest)
        && TemplatePngFiles.Same(value.EvidencePolicyDigest, contact.PolicyDigest)
        && value.EvidenceVersion == contact.Version
        && value.Statistics is not null
        && value.RidgeEvidence is not null
        && contact.Components.Any(component =>
            component.Original.Area == value.RidgeEvidence.ObservedArea
            && component.SupportedArea >= value.Statistics.MeasuredSupportArea
        );

    private static bool Shape(
        TemplateContourManifest value,
        TemplateProcessingValidated processing
    ) =>
        value.Version == 1
        && value.Planes is not null
        && value.Planes.Valid(processing.Manifest.Masks)
        && value.RidgeEvidence is not null
        && TemplatePngFiles.Same(
            value.RidgeEvidence.ObservedContactDigest,
            value.Planes.ObservedContact.PixelSha256
        )
        && value.Policy is not null
        && value.Statistics is not null
        && value.AppliedGuard
            == (processing.Policy.IsOriginal ? 0 : value.Policy.ModifiedBoundaryGuard)
        && TemplatePngFiles.Same(value.SourceDigest, processing.Manifest.Original.PixelSha256)
        && TemplatePngFiles.Same(value.ProcessingIdentity, processing.Manifest.Identity)
        && TemplatePngFiles.Hash(value.EvidencePolicyDigest)
        && TemplatePngFiles.Hash(value.PolicyDigest)
        && !string.IsNullOrWhiteSpace(value.AlgorithmVersion)
        && !string.IsNullOrWhiteSpace(value.EvidenceVersion);

    private static DesktopError Invalid() =>
        TemplatePngFiles.Invalid(
            "Contact contour metadata contradicts its original source, measured evidence, or current context."
        );
}
