// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class TemplateContactValidation
{
    internal static bool Shape(TemplateContactManifest value, int pixels) =>
        TemplatePngFiles.Hash(value.SourceDigest)
        && TemplatePngFiles.Hash(value.PolicyDigest)
        && !string.IsNullOrWhiteSpace(value.Version)
        && value.Policy is not null
        && value.AcceptedStatistics is not null
        && value.ObservedStatistics is not null
        && value.AcceptedStatistics.Valid(pixels)
        && value.ObservedStatistics.Valid(pixels)
        && !value.Components.IsDefault
        && value.Components.All(component => component is not null && component.Valid(pixels))
        && value.ComponentCount >= 0
        && value.ComponentCount <= value.Components.Length
        && value.CandidateArea == value.Components.Sum(component => (long)component.Original.Area)
        && value.RidgeSupportArea
            == value.Components.Sum(component => (long)component.SupportedArea)
        && value.AcceptedArea == value.AcceptedStatistics.Area
        && value.RejectedArea == value.CandidateArea - value.AcceptedArea
        && Enum.TryParse<Fs50ContactIssue>(value.Issue, out var issue)
        && Enum.IsDefined(issue);

    internal static Result<TemplateContactDetails, DesktopError> Check(
        TemplateContactManifest metadata,
        TemplateProcessingValidated processing,
        FingerprintProcessingMask mask,
        ImmutableArray<byte> candidate,
        ImmutableArray<byte> ridge,
        ImmutableArray<byte> rejected
    )
    {
        return Metadata(metadata, processing)
            .Ensure(
                _ =>
                    TemplatePngFiles.Same(
                        metadata.SourceDigest,
                        processing.Manifest.Original.PixelSha256
                    )
                    && candidate.Count(value => value == 1) == metadata.CandidateArea
                    && ridge.Count(value => value == 1) == metadata.RidgeSupportArea
                    && rejected.Count(value => value == 1) == metadata.RejectedArea
                    && mask.Retained.Count(value => value == 1) == metadata.AcceptedArea
                    && Planes(candidate, ridge, rejected, mask),
                TemplatePngFiles.Invalid(
                    "Contact planes do not match their declared source or statistics."
                )
            )
            .Map(value => Details(metadata, value, mask, candidate, ridge, rejected));
    }

    internal static Result<Fs50EvidencePolicy, DesktopError> Metadata(
        TemplateContactManifest metadata,
        TemplateProcessingValidated processing
    ) =>
        Fs50EvidencePolicy
            .Create(metadata.Policy)
            .MapError(error => TemplatePngFiles.Invalid(error.Message))
            .Check(value => PolicyDigest(value, metadata))
            .Ensure(
                _ =>
                    TemplatePngFiles.Same(
                        metadata.SourceDigest,
                        processing.Manifest.Original.PixelSha256
                    ),
                TemplatePngFiles.Invalid("Contact metadata does not belong to its original image.")
            );

    private static TemplateContactDetails Details(
        TemplateContactManifest metadata,
        Fs50EvidencePolicy policy,
        FingerprintProcessingMask mask,
        ImmutableArray<byte> candidate,
        ImmutableArray<byte> ridge,
        ImmutableArray<byte> rejected
    ) =>
        new(
            candidate,
            ridge,
            mask.Retained,
            rejected,
            mask.Inner,
            metadata.SourceDigest,
            metadata.PolicyDigest,
            metadata.Version,
            policy,
            Summary(metadata)
        );

    private static TemplateContactSummary Summary(TemplateContactManifest value) =>
        new(
            value.CandidateArea,
            value.RidgeSupportArea,
            value.AcceptedArea,
            value.RejectedArea,
            value.ComponentCount,
            Enum.Parse<Fs50ContactIssue>(value.Issue)
        );

    private static UnitResult<DesktopError> PolicyDigest(
        Fs50EvidencePolicy policy,
        TemplateContactManifest metadata
    )
    {
        return
            metadata.Version == Fs50EvidencePolicy.Version
            && TemplatePngFiles.Same(policy.Digest, metadata.PolicyDigest)
            ? UnitResult.Success<DesktopError>()
            : TemplatePngFiles.Invalid("The contact version or policy digest does not match.");
    }

    private static bool Planes(
        ImmutableArray<byte> candidate,
        ImmutableArray<byte> ridge,
        ImmutableArray<byte> rejected,
        FingerprintProcessingMask mask
    ) =>
        Enumerable
            .Range(0, candidate.Length)
            .All(index =>
                candidate[index] <= 1
                && ridge[index] <= candidate[index]
                && mask.Retained[index] <= candidate[index]
                && rejected[index] == candidate[index] - mask.Retained[index]
            );
}
