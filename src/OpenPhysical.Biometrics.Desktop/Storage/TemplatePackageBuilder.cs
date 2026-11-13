// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal static class TemplatePackageBuilder
{
    internal static Result<ImmutableArray<TemplatePackageFile>, DesktopError> Create(
        FingerprintRecord record,
        ImmutableArray<CapturedFingerprint> captures
    ) => Create(record, captures, TemplateSaveOptions.Default);

    internal static Result<ImmutableArray<TemplatePackageFile>, DesktopError> Create(
        FingerprintRecord record,
        ImmutableArray<CapturedFingerprint> captures,
        TemplateSaveOptions options
    )
    {
        if (
            captures.Any(capture =>
                capture.ExtractorVersion.Match(string.IsNullOrWhiteSpace, () => false)
            )
        )
            return TemplatePngFiles.Invalid("The capture extractor identity must not be blank.");
        var expected = UnsignedTemplateService.Create(
            captures,
            new TemplateMetadata(record.ProductOwner, record.ProductType, record.CaptureEquipment)
        );
        if (expected.IsFailure)
            return expected.Error;
        var encoded = Incits378Codec.Write(record);
        if (
            encoded.IsFailure
            || !encoded.Value.SequenceEqual(Incits378Codec.Write(expected.Value).Value)
        )
            return new DesktopError(
                "Storage.Association",
                "The template does not contain the selected captured views."
            );
        return CreateFiles(encoded.Value, captures, options);
    }

    private static Result<ImmutableArray<TemplatePackageFile>, DesktopError> CreateFiles(
        byte[] encoded,
        ImmutableArray<CapturedFingerprint> captures,
        TemplateSaveOptions options
    )
    {
        var files = ImmutableArray.CreateBuilder<TemplatePackageFile>();
        var images = ImmutableArray.CreateBuilder<TemplateManifestImage>();
        files.Add(new TemplatePackageFile("fingerprints.fmr", encoded));
        for (var index = 0; index < captures.Length; index++)
        {
            var capture = captures[index];
            var image = AddImage(files, index, captures[index], options);
            if (image.IsFailure)
                return image.Error;
            images.Add(image.Value);
        }
        var manifest = new TemplateManifest(2, TemplateSha256.Of(encoded), images.ToImmutable());
        files.Add(
            new TemplatePackageFile(
                "fingerprints.json",
                JsonSerializer.SerializeToUtf8Bytes(
                    manifest,
                    new JsonSerializerOptions { WriteIndented = true }
                )
            )
        );
        return files.ToImmutable();
    }

    private static Result<TemplateManifestImage, DesktopError> AddImage(
        ImmutableArray<TemplatePackageFile>.Builder files,
        int index,
        CapturedFingerprint capture,
        TemplateSaveOptions options
    )
    {
        if (capture.Processing.HasNoValue)
            return TemplatePngFiles.Invalid(
                "A Studio capture package requires processing evidence."
            );
        var features = TemplateFeatureAdmission.Check(
            capture.View,
            capture.Processing.Value.Artifact.Mask
        );
        if (features.IsFailure)
            return features.Error;
        var pixels = capture.Processing.Value.Artifact.Pixels;
        return TemplatePngFiles
            .Add(
                files,
                $"view-{index}-finger-{(byte)capture.Finger}-processing.png",
                pixels,
                capture.Frame.Width,
                capture.Frame.Height
            )
            .Map(png => CreateImage(index, capture, png))
            .Bind(image =>
                TemplateProcessingWriter
                    .Add(files, capture, image, options)
                    .Map(processing => image with { Processing = processing })
            );
    }

    private static TemplateManifestImage CreateImage(
        int index,
        CapturedFingerprint capture,
        TemplatePngDescriptor png
    ) =>
        new(
            index,
            (byte)capture.Finger,
            png.FileName,
            png.Sha256,
            png.PixelSha256,
            capture.Frame.Width,
            capture.Frame.Height,
            capture.Frame.CapturedAt,
            capture.Frame.Sequence,
            TemplateProvenance.For(capture.Frame),
            TemplateNfiq1Metadata.Write(capture.Quality)
        )
        {
            Nfiq2 = TemplateUnifiedQualityMetadata.Write(capture.Nfiq2),
            ExtractorVersion = TemplateExtractorMetadata.Write(capture.ExtractorVersion),
        };
}
