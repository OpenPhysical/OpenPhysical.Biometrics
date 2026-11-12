// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal static class TemplateManifestValidation
{
    internal static UnitResult<DesktopError> Check(
        TemplateManifest manifest,
        FingerprintRecord record,
        byte[] bytes
    )
    {
        if (
            !ValidSubjects(manifest)
            || manifest.Version != 2
            || manifest.TemplateSha256 != TemplateSha256.Of(bytes)
            || manifest.Images.IsDefaultOrEmpty
            || manifest.Images.Length != record.Views.Length
            || manifest.Images.Length > 2
            || manifest.Images.Any(image => image is null)
            || manifest.Images.Select(image => image.ViewIndex).Distinct().Count()
                != manifest.Images.Length
            || manifest
                .Images.Select(image => image.FileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != manifest.Images.Length
            || !manifest.Images.All(image => ValidImage(image, record))
            || manifest.Images.Max(image => image.Width) != record.Width
            || manifest.Images.Max(image => image.Height) != record.Height
        )
            return UnitResult.Failure(
                new DesktopError(
                    "Storage.Manifest",
                    "The capture manifest is invalid or does not match the template."
                )
            );
        return SupplementalQuality(manifest)
            .Bind(() => TemplateManifestCompanions.Check(manifest, record));
    }

    private static UnitResult<DesktopError> SupplementalQuality(TemplateManifest manifest)
    {
        foreach (var image in manifest.Images)
        {
            var assessment = TemplateUnifiedQualityMetadata.Read(image.Nfiq2);
            if (assessment.IsFailure)
                return assessment.Error;
            var extractor = TemplateExtractorMetadata.Read(image.ExtractorVersion);
            if (extractor.IsFailure)
                return extractor.Error;
        }
        return UnitResult.Success<DesktopError>();
    }

    private static bool ValidImage(TemplateManifestImage image, FingerprintRecord record) =>
        image.ViewIndex >= 0
        && image.ViewIndex < record.Views.Length
        && image.Finger == record.Views[image.ViewIndex].FingerPosition
        && image.Width > 0
        && image.Height > 0
        && image.Width <= record.Width
        && image.Height <= record.Height
        && (long)image.Width * image.Height <= 16 * 1024 * 1024
        && image.CapturedAtUtc != default
        && image.CapturedAtUtc.Offset == TimeSpan.Zero
        && image.FrameSequence >= 0
        && ValidProvenance(image)
        && image.Quality.ValueKind == JsonValueKind.Object
        && ValidHash(image.Sha256)
        && ValidHash(image.PixelSha256)
        && SafeName(image.FileName)
        && record
            .Views[image.ViewIndex]
            .Minutiae.All(point => point.X < image.Width && point.Y < image.Height);

    private static bool SafeName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && !Path.IsPathRooted(name)
        && name == Path.GetFileName(name)
        && !name.Contains('\\')
        && name.EndsWith(".png", StringComparison.Ordinal);

    private static bool ValidHash(string hash) =>
        hash is { Length: 64 } && hash.All(Uri.IsHexDigit);

    private static bool ValidSubjects(TemplateManifest manifest) =>
        !manifest.Subjects.IsDefault
        && manifest.Subjects.Length <= 1
        && manifest.Subjects.All(person =>
            person is not null
            && CapturePerson.Create(person.DisplayName, person.SubjectIdentifier).IsSuccess
        );

    private static bool ValidProvenance(TemplateManifestImage image) =>
        image.Provenance.ValueKind == JsonValueKind.Object
        && image.Provenance.TryGetProperty("OutputGeometry", out var geometry)
        && geometry.ValueKind == JsonValueKind.Object
        && geometry.TryGetProperty("Width", out var width)
        && width.ValueKind == JsonValueKind.Number
        && width.TryGetInt32(out var x)
        && x == image.Width
        && geometry.TryGetProperty("Height", out var height)
        && height.ValueKind == JsonValueKind.Number
        && height.TryGetInt32(out var y)
        && y == image.Height;
}
