// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class TemplateContourReader
{
    internal static async Task<Result<TemplateImage, DesktopError>> AttachAsync(
        TemplateCompanionSource source,
        TemplateProcessingValidated processing,
        TemplateManifestImage entry,
        TemplateImage image,
        CancellationToken token
    )
    {
        var parsed = TemplateContourValidation.Read(processing, entry);
        if (parsed.IsFailure)
            return parsed.Error;
        var planes = await TemplatePngFiles
            .ReadAllAsync(source, parsed.Value.Manifest.Planes.Additional, 800, 750, token)
            .ConfigureAwait(false);
        if (planes.IsFailure)
            return planes.Error;
        return Restore(parsed.Value, image.Processing.Value, planes.Value, token)
            .Bind(value =>
                TemplateOriginalQualification.Attach(
                    processing.Manifest,
                    entry,
                    value,
                    parsed.Value.Manifest.RidgeEvidence
                )
            )
            .Map(value => image with { Processing = value });
    }

    private static Result<TemplateProcessingDetails, DesktopError> Restore(
        TemplateContourValidated value,
        TemplateProcessingDetails processing,
        ImmutableArray<ImmutableArray<byte>> planes,
        CancellationToken token
    ) =>
        Fs50ContactContourArtifact
            .Restore(
                value.Manifest.SourceDigest,
                value.Manifest.EvidenceVersion,
                value.Manifest.EvidencePolicyDigest,
                value.Policy,
                new Fs50ContactContourPlanes(
                    planes[0],
                    planes[1],
                    planes[2],
                    planes[3],
                    processing.Mask.Retained,
                    planes[4]
                ),
                value.Manifest.Statistics,
                token
            )
            .MapError(error => TemplatePngFiles.Invalid(error.Message))
            .Bind(contour => ValidateRegion(contour, value, processing, planes[5]))
            .Map(result => result with { ObservedContactMask = planes[6] })
            .Check(result => ContactMatches(result, processing.ContactDiagnostics))
            .Check(result =>
                TemplateObservedContact.Check(value.Manifest, result.Contour.Value, planes[6])
            );

    private static Result<TemplateProcessingDetails, DesktopError> ValidateRegion(
        Fs50ContactContourArtifact contour,
        TemplateContourValidated value,
        TemplateProcessingDetails processing,
        ImmutableArray<byte> guarded
    ) =>
        contour
            .ToProcessingRegion(!processing.Policy.IsOriginal)
            .MapError(error => TemplatePngFiles.Invalid(error.Message))
            .Ensure(
                region =>
                    region.AppliedGuard == value.Manifest.AppliedGuard
                    && region.GuardedContextMask.SequenceEqual(guarded)
                    && region.Mask.Digest == processing.Mask.Digest,
                TemplatePngFiles.Invalid(
                    "Saved feature eligibility does not match its guarded context."
                )
            )
            .Map(region => processing with { Contour = contour, ProcessingRegion = region });

    private static UnitResult<DesktopError> ContactMatches(
        TemplateProcessingDetails processing,
        Maybe<TemplateContactDetails> diagnostics
    )
    {
        if (diagnostics.HasNoValue || diagnostics.Value.RidgeSupportMask.HasNoValue)
            return UnitResult.Success<DesktopError>();
        var contour = processing.Contour.Value;
        var ridge = diagnostics.Value.RidgeSupportMask.Value;
        var candidate = diagnostics.Value.CandidateMask.Value;
        return Enumerable
            .Range(0, 600000)
            .All(index =>
                contour.ContactMask[index] <= candidate[index]
                && processing.ObservedContactMask[index] <= candidate[index]
                && contour.SupportMask[index]
                    == (contour.ContactMask[index] != 0 && ridge[index] != 0 ? 1 : 0)
            )
            ? UnitResult.Success<DesktopError>()
            : TemplatePngFiles.Invalid(
                "Selected contact and support contradict their diagnostic source planes."
            );
    }
}
