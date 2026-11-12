// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class TemplateContactReader
{
    internal static async Task<Result<TemplateImage, DesktopError>> AttachAsync(
        string folder,
        TemplateProcessingValidated value,
        TemplateImage image,
        CancellationToken token
    )
    {
        var parsed = Parse(value.Manifest.ContactDiagnostics, image.Width * image.Height);
        if (parsed.IsFailure)
            return parsed.Error;
        var metadata = parsed.Value;
        if (metadata.Planes.ValueKind == JsonValueKind.Undefined)
            return WithoutPlanes(metadata, value, image);
        return await WithPlanes(folder, value, image, metadata, token).ConfigureAwait(false);
    }

    private static async Task<Result<TemplateImage, DesktopError>> WithPlanes(
        string folder,
        TemplateProcessingValidated value,
        TemplateImage image,
        TemplateContactManifest metadata,
        CancellationToken token
    )
    {
        var descriptors = Planes(metadata.Planes).Bind(TemplateContactPlaneFiles.All);
        if (descriptors.IsFailure)
            return descriptors.Error;
        var planes = await TemplatePngFiles
            .ReadAllAsync(folder, descriptors.Value, image.Width, image.Height, token)
            .ConfigureAwait(false);
        if (planes.IsFailure)
            return planes.Error;
        return ContactMask(planes.Value, image)
            .Bind(mask =>
                TemplateContactValidation.Check(
                    metadata,
                    value,
                    mask,
                    planes.Value[0],
                    planes.Value[1],
                    planes.Value[2]
                )
            )
            .Map(details => WithDetails(image, details));
    }

    private static Result<
        OpenPhysical.Biometrics.Capture.FingerprintProcessingMask,
        DesktopError
    > ContactMask(ImmutableArray<ImmutableArray<byte>> planes, TemplateImage image) =>
        OpenPhysical
            .Biometrics.Capture.FingerprintProcessingMask.Create(
                image.Width,
                image.Height,
                planes[3].AsSpan(),
                planes[4].AsSpan()
            )
            .MapError(error => TemplatePngFiles.Invalid(error.Message));

    private static TemplateImage WithDetails(TemplateImage image, TemplateContactDetails details) =>
        image with
        {
            Processing = image.Processing.Value with { ContactDiagnostics = details },
        };

    private static Result<TemplateImage, DesktopError> WithoutPlanes(
        TemplateContactManifest metadata,
        TemplateProcessingValidated value,
        TemplateImage image
    ) =>
        TemplateContactValidation
            .Metadata(metadata, value)
            .Map(policy =>
                image with
                {
                    Processing = image.Processing.Value with
                    {
                        ContactDiagnostics = new TemplateContactDetails(
                            default,
                            default,
                            default,
                            default,
                            default,
                            metadata.SourceDigest,
                            metadata.PolicyDigest,
                            metadata.Version,
                            policy,
                            Summary(metadata)
                        ),
                    },
                }
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

    internal static Result<TemplateContactPlanes, DesktopError> Planes(JsonElement element) =>
        DesktopEdge
            .Try(
                () => element.Deserialize<TemplateContactPlanes>(),
                "Storage.Manifest",
                "Diagnostic plane metadata is invalid."
            )
            .Bind(value =>
                value is not null
                && TemplatePngFiles.Valid(value.Candidate)
                && TemplatePngFiles.Valid(value.RidgeSupport)
                && TemplatePngFiles.Valid(value.Rejected)
                    ? Result.Success<TemplateContactPlanes, DesktopError>(value)
                    : TemplatePngFiles.Invalid("Advertised diagnostic planes are invalid.")
            );

    internal static Result<TemplateContactManifest, DesktopError> Parse(
        JsonElement element,
        int pixels
    )
    {
        if (element.ValueKind != JsonValueKind.Object)
            return TemplatePngFiles.Invalid("Advertised contact diagnostics must be an object.");
        return DesktopEdge
            .Try(
                () => element.Deserialize<TemplateContactManifest>(),
                "Storage.Manifest",
                "Contact evidence could not be parsed."
            )
            .Bind(value =>
                value is not null && TemplateContactValidation.Shape(value, pixels)
                    ? Result.Success<TemplateContactManifest, DesktopError>(value)
                    : TemplatePngFiles.Invalid("Contact diagnostic metadata is invalid.")
            );
    }
}
