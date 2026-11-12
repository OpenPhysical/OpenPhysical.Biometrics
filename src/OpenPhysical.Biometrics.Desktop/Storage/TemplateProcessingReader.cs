// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;

internal static class TemplateProcessingReader
{
    internal static async Task<Result<TemplateImage, DesktopError>> AttachAsync(
        string folder,
        TemplateManifestImage entry,
        TemplateImage image,
        CancellationToken token
    )
    {
        var parsed = TemplateProcessingValidation.Read(entry.Processing, entry);
        if (parsed.IsFailure)
            return parsed.Error;
        var value = parsed.Value;
        var planes = await TemplatePngFiles
            .ReadAllAsync(
                folder,
                [
                    value.Manifest.Original,
                    value.Manifest.Masks.Retained,
                    value.Manifest.Masks.Inner,
                ],
                image.Width,
                image.Height,
                token
            )
            .ConfigureAwait(false);
        if (planes.IsFailure)
            return planes.Error;
        var attached = BuildImage(value, entry, image, planes.Value);
        return attached.IsFailure
            ? attached.Error
            : await AttachSupplementalAsync(folder, value, entry, attached.Value, token)
                .ConfigureAwait(false);
    }

    private static async Task<Result<TemplateImage, DesktopError>> AttachSupplementalAsync(
        string folder,
        TemplateProcessingValidated value,
        TemplateManifestImage entry,
        TemplateImage image,
        CancellationToken token
    )
    {
        var contact = await TemplateContactReader
            .AttachAsync(folder, value, image, token)
            .ConfigureAwait(false);
        if (contact.IsFailure)
            return contact.Error;
        return await TemplateContourReader
            .AttachAsync(folder, value, entry, contact.Value, token)
            .ConfigureAwait(false);
    }

    private static Result<TemplateImage, DesktopError> BuildImage(
        TemplateProcessingValidated value,
        TemplateManifestImage entry,
        TemplateImage image,
        ImmutableArray<ImmutableArray<byte>> planes
    ) =>
        FingerprintProcessingMask
            .Create(image.Width, image.Height, planes[1].AsSpan(), planes[2].AsSpan())
            .MapError(error => TemplatePngFiles.Invalid(error.Message))
            .Ensure(
                mask => TemplatePngFiles.Same(mask.Digest, value.Manifest.Masks.Digest),
                TemplatePngFiles.Invalid(
                    "The processing mask identity does not match its companions."
                )
            )
            .Check(mask => Identity(value, entry, mask))
            .Map(mask => image with { Processing = Details(value, image, planes[0], mask) });

    private static UnitResult<DesktopError> Identity(
        TemplateProcessingValidated value,
        TemplateManifestImage image,
        FingerprintProcessingMask mask
    )
    {
        var result = FingerprintPreprocessingIdentity
            .Create(
                value.Source,
                value.Manifest.Original.PixelSha256,
                image.PixelSha256,
                mask.Digest,
                value.Manifest.PolicyDigest,
                value.Manifest.ProcessingRevision,
                value.Manifest.AlgorithmVersion
            )
            .MapError(error => TemplatePngFiles.Invalid(error.Message))
            .Ensure(
                identity => TemplatePngFiles.Same(identity, value.Manifest.Identity),
                TemplatePngFiles.Invalid(
                    "The original, processing image, mask, and policy do not share the saved artifact identity."
                )
            );
        return result.IsFailure ? result.Error : UnitResult.Success<DesktopError>();
    }

    private static TemplateProcessingDetails Details(
        TemplateProcessingValidated value,
        TemplateImage image,
        ImmutableArray<byte> original,
        FingerprintProcessingMask mask
    ) =>
        new(
            image with
            {
                GrayPixels = original,
                Nfiq2 = value.OriginalAssessment.Nfiq2,
                Processing = Maybe<TemplateProcessingDetails>.None,
                ExtractorVersion = Maybe<string>.None,
            },
            value.OriginalAssessment,
            value.ProcessedAssessment,
            mask,
            value.Policy,
            value.SelectionPolicy,
            value.SelectionReason,
            value.Candidates,
            value.Source,
            value.Manifest.AlgorithmVersion,
            value.Manifest.ProcessingRevision,
            value.Manifest.Identity
        )
        {
            OriginalAcquisitionPolicy = value.OriginalAcquisitionPolicy,
        };
}
