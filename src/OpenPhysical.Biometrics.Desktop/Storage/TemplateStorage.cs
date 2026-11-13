// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Saves private Studio capture packages and opens templates with verified companions.</summary>
public sealed class TemplateStorage : ITemplateStorage
{
    /// <summary>Gets the durable per-user default capture directory.</summary>
    public static string DefaultFolder =>
        Path.Combine(DesktopPaths.UserData, "Private Biometrics", "Captures");

    /// <inheritdoc cref="ITemplateStorage.SaveAsync"/>
    public Task<Result<string, DesktopError>> SaveAsync(
        FingerprintRecord record,
        ImmutableArray<CapturedFingerprint> captures,
        string folder,
        CancellationToken cancellationToken
    ) =>
        SavePackageAsync(
            TemplatePackageBuilder.Create(record, captures),
            folder,
            cancellationToken
        );

    /// <inheritdoc cref="ITemplateStorage.SaveAsync"/>
    public Task<Result<string, DesktopError>> SaveAsync(
        FingerprintRecord record,
        ImmutableArray<CapturedFingerprint> captures,
        string folder,
        TemplateSaveOptions options,
        CancellationToken cancellationToken
    ) =>
        SavePackageAsync(
            TemplatePackageBuilder.Create(record, captures, options),
            folder,
            cancellationToken
        );

    private static async Task<Result<string, DesktopError>> SavePackageAsync(
        Result<ImmutableArray<TemplatePackageFile>, DesktopError> package,
        string folder,
        CancellationToken cancellationToken
    )
    {
        if (package.IsFailure)
            return package.Error;
        return await PrivatePackageFiles
            .WriteAsync(package.Value, folder, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<TemplateDocument, DesktopError>> LoadAsync(
        string fmrPath,
        CancellationToken cancellationToken
    )
    {
        var bytes = await TemplateFileReader
            .ReadAsync(fmrPath, 16 * 1024 * 1024, cancellationToken)
            .ConfigureAwait(false);
        if (bytes.IsFailure)
            return bytes.Error;
        var parsed = TemplateRecordReader.Parse(bytes.Value);
        if (parsed.IsFailure)
            return parsed.Error;
        return await TemplateManifestReader
            .LoadAsync(fmrPath, bytes.Value, parsed.Value, cancellationToken)
            .ConfigureAwait(false);
    }
}
