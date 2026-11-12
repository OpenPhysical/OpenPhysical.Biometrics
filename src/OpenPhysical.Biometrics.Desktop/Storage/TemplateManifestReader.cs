// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal static class TemplateManifestReader
{
    internal static async Task<Result<TemplateDocument, DesktopError>> LoadAsync(
        string path,
        byte[] bytes,
        FingerprintRecord record,
        CancellationToken cancellationToken
    )
    {
        var manifestPath = Path.ChangeExtension(path, ".json");
        if (!File.Exists(manifestPath))
            return new TemplateDocument(record, []);
        var input = await TemplateFileReader
            .ReadAsync(manifestPath, 256 * 1024, cancellationToken)
            .ConfigureAwait(false);
        if (input.IsFailure)
            return input.Error;
        var parsed = Parse(input.Value);
        if (parsed.IsFailure)
            return parsed.Error;
        var validated = TemplateManifestValidation.Check(parsed.Value, record, bytes);
        if (validated.IsFailure)
            return validated.Error;
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is not { } folder)
            return new DesktopError(
                "Storage.Manifest",
                "The template must have a containing directory."
            );
        var images = await LoadImagesAsync(folder, parsed.Value.Images, cancellationToken)
            .ConfigureAwait(false);
        return images
            .Check(value => TemplateFeatureAdmission.Check(record, value))
            .Map(value => new TemplateDocument(record, value) { Subjects = parsed.Value.Subjects });
    }

    private static Result<TemplateManifest, DesktopError> Parse(byte[] bytes)
    {
        var parsed = DesktopEdge.Try(
            () => JsonSerializer.Deserialize<TemplateManifest>(bytes),
            "Storage.Manifest",
            "The capture manifest could not be parsed."
        );
        if (parsed.IsFailure)
            return parsed.Error;
        return parsed.Value is { } manifest
            ? manifest
            : new DesktopError("Storage.Manifest", "The capture manifest is empty.");
    }

    private static async Task<Result<ImmutableArray<TemplateImage>, DesktopError>> LoadImagesAsync(
        string folder,
        ImmutableArray<TemplateManifestImage> entries,
        CancellationToken cancellationToken
    )
    {
        var images = ImmutableArray.CreateBuilder<TemplateImage>();
        foreach (var entry in entries.OrderBy(image => image.ViewIndex))
        {
            var image = await LoadImageAsync(folder, entry, cancellationToken)
                .ConfigureAwait(false);
            if (image.IsFailure)
                return image.Error;
            images.Add(image.Value);
        }
        return images.ToImmutable();
    }

    private static async Task<Result<TemplateImage, DesktopError>> LoadImageAsync(
        string folder,
        TemplateManifestImage entry,
        CancellationToken cancellationToken
    )
    {
        var input = await TemplatePngFiles
            .ReadAsync(
                folder,
                new(entry.FileName, entry.Sha256, entry.PixelSha256),
                entry.Width,
                entry.Height,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (input.IsFailure)
            return input.Error;
        var image = DecodeImage(input.Value, entry);
        return image.IsFailure
            ? image.Error
            : await TemplateProcessingReader
                .AttachAsync(folder, entry, image.Value, cancellationToken)
                .ConfigureAwait(false);
    }

    private static Result<TemplateImage, DesktopError> DecodeImage(
        ImmutableArray<byte> pixels,
        TemplateManifestImage entry
    ) =>
        TemplateUnifiedQualityMetadata
            .Read(entry.Nfiq2)
            .Bind(
                (Maybe<FingerprintUnifiedQualityAssessment> assessment) =>
                    TemplateExtractorMetadata
                        .Read(entry.ExtractorVersion)
                        .Map(
                            (Maybe<string> extractor) =>
                                new TemplateImage(
                                    entry.ViewIndex,
                                    (FingerPosition)entry.Finger,
                                    entry.Width,
                                    entry.Height,
                                    pixels,
                                    entry.CapturedAtUtc
                                )
                                {
                                    Nfiq2 = assessment,
                                    ExtractorVersion = extractor,
                                }
                        )
            );
}
