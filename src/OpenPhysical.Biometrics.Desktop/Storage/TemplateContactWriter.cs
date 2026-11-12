// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class TemplateContactWriter
{
    internal static Result<JsonElement, DesktopError> Add(
        ImmutableArray<TemplatePackageFile>.Builder files,
        string stem,
        CaptureProcessingEvidence evidence,
        TemplateSaveOptions options
    )
    {
        var value = evidence.ContactDiagnostics;
        var artifact = evidence.Artifact;
        if (value.SourceDigest != artifact.OriginalDigest)
            return TemplatePngFiles.Invalid(
                "Contact diagnostics do not belong to the saved processing image."
            );
        var manifest = Manifest(value);
        return options.IncludeDiagnostics
            ? AddPlanes(files, stem, evidence)
                .Map(planes => JsonSerializer.SerializeToElement(manifest with { Planes = planes }))
            : JsonSerializer.SerializeToElement(manifest);
    }

    private static Result<JsonElement, DesktopError> AddPlanes(
        ImmutableArray<TemplatePackageFile>.Builder files,
        string stem,
        CaptureProcessingEvidence evidence
    )
    {
        var artifact = evidence.Artifact;
        var value = evidence.ContactDiagnostics;
        var images = ImmutableArray.CreateBuilder<(string Name, ImmutableArray<byte> Pixels)>();
        images.Add((stem + "-candidate.png", value.CandidateMask));
        images.Add((stem + "-ridge-support.png", value.RidgeSupportMask));
        images.Add((stem + "-rejected.png", value.RejectedMask));
        images.Add((stem + "-contact-accepted.png", value.AcceptedMask));
        images.Add((stem + "-contact-inner.png", value.InnerMask));
        return TemplatePngFiles
            .AddAll(files, artifact.Width, artifact.Height, images.ToImmutable())
            .Map(planes => JsonSerializer.SerializeToElement(Descriptors(planes)));
    }

    private static TemplateContactPlanes Descriptors(
        ImmutableArray<TemplatePngDescriptor> planes
    ) => new(planes[0], planes[1], planes[2], planes[3], planes[4]);

    private static TemplateContactManifest Manifest(Fs50ContactDiagnostics value) =>
        new(
            value.SourceDigest,
            value.PolicyDigest,
            value.Version,
            value.Policy.Options,
            value.CandidateArea,
            value.RidgeSupportArea,
            value.AcceptedArea,
            value.RejectedArea,
            value.ComponentCount,
            value.Issue.ToString(),
            TemplateContactStatistics.From(value.AcceptedStatistics),
            TemplateContactStatistics.From(value.ObservedStatistics),
            value.Components.Select(TemplateContactComponent.From).ToImmutableArray()
        );
}
