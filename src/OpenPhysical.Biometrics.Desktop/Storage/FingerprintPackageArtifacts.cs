// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

internal static class FingerprintPackageArtifacts
{
    internal static Result<ImmutableArray<PackageArtifact>, DesktopError> Create(
        ImmutableArray<CapturedFingerprint> captures,
        TemplateMetadata metadata,
        TemplateSaveOptions options
    ) =>
        UnsignedTemplateService
            .Create(captures, metadata)
            .Bind(record => TemplatePackageBuilder.Create(record, captures, options))
            .Bind(Convert);

    private static Result<ImmutableArray<PackageArtifact>, DesktopError> Convert(
        ImmutableArray<TemplatePackageFile> files
    )
    {
        var artifacts = ImmutableArray.CreateBuilder<PackageArtifact>();
        foreach (var file in files)
        {
            var artifact = PackageArtifact.Create(
                file.Name,
                file.Bytes,
                "fingerprints",
                Format(file.Name)
            );
            if (artifact.IsFailure)
                return new DesktopError(artifact.Error.Code, artifact.Error.Message);
            artifacts.Add(artifact.Value);
        }
        return artifacts.ToImmutable();
    }

    private static string Format(string name) =>
        Path.GetExtension(name) switch
        {
            ".fmr" => "INCITS378-2004",
            ".png" => "PNG",
            _ => "JSON",
        };
}
