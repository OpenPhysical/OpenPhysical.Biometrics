// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class TemplateContourWriter
{
    internal static Result<JsonElement, DesktopError> Add(
        ImmutableArray<TemplatePackageFile>.Builder files,
        string stem,
        CaptureProcessingEvidence evidence,
        TemplateProcessingMasks processing
    )
    {
        var region = evidence.Contour.ToProcessingRegion(!evidence.Artifact.Policy.IsOriginal);
        if (region.IsFailure || !Matches(evidence, region.Value))
            return TemplatePngFiles.Invalid(
                "Contact, feature admission, and processing context belong to different evidence."
            );
        var contour = evidence.Contour;
        return TemplatePngFiles
            .AddAll(
                files,
                800,
                750,
                [
                    (stem + "-contact.png", contour.ContactMask),
                    (stem + "-filled-holes.png", contour.FilledHoleMask),
                    (stem + "-features.png", contour.FeatureMask),
                    (stem + "-support.png", contour.SupportMask),
                    (stem + "-feature-uncertainty.png", contour.UncertainMask),
                    (stem + "-guarded-context.png", evidence.ProcessingRegion.GuardedContextMask),
                    (
                        stem + "-observed-contact.png",
                        evidence.ContactDiagnostics.ObservedContactMask
                    ),
                ]
            )
            .Map(planes =>
                JsonSerializer.SerializeToElement(Manifest(evidence, planes, processing))
            );
    }

    private static bool Matches(
        CaptureProcessingEvidence evidence,
        Fs50ContourProcessingRegion expected
    ) =>
        TemplatePngFiles.Same(evidence.Contour.SourceDigest, evidence.Artifact.OriginalDigest)
        && TemplatePngFiles.Same(
            evidence.Contour.EvidencePolicyDigest,
            evidence.ContactDiagnostics.PolicyDigest
        )
        && evidence.Contour.EvidenceVersion == evidence.ContactDiagnostics.Version
        && evidence.ProcessingRegion.AppliedGuard == expected.AppliedGuard
        && evidence.ProcessingRegion.Mask.Digest == expected.Mask.Digest
        && evidence.Artifact.Mask.Digest == expected.Mask.Digest
        && evidence.ProcessingRegion.GuardedContextMask.SequenceEqual(expected.GuardedContextMask)
        && TemplatePngFiles.Same(
            evidence.ProcessingRegion.GuardedContextDigest,
            expected.GuardedContextDigest
        );

    private static TemplateContourManifest Manifest(
        CaptureProcessingEvidence evidence,
        ImmutableArray<TemplatePngDescriptor> planes,
        TemplateProcessingMasks processing
    )
    {
        var contour = evidence.Contour;
        return new(
            1,
            new(
                planes[0],
                planes[1],
                planes[2],
                planes[3],
                processing.Retained,
                planes[4],
                planes[5],
                planes[6]
            ),
            contour.SourceDigest,
            contour.EvidencePolicyDigest,
            contour.EvidenceVersion,
            TemplateContourPolicyMetadata.From(contour.Policy),
            contour.PolicyDigest,
            contour.Version,
            contour.Statistics,
            evidence.ProcessingRegion.AppliedGuard,
            evidence.Artifact.Identity,
            TemplateObservedContact.From(evidence.ContactDiagnostics)
        );
    }
}
