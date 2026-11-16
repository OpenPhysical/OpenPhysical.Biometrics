// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Shared private staging, flush, installation, and rollback for explicit file outputs.</summary>
public static class PrivateFileTransaction
{
    /// <summary>Stages every output privately before installation; restores existing contents if any installation fails.</summary>
    /// <param name="files">Distinct final paths and their immutable contents.</param>
    /// <param name="overwrite">Whether existing regular files may be replaced.</param>
    /// <param name="token">Cancellation, checked before staging and each installation.</param>
    /// <returns>Complete installation or a typed failure; no plaintext package archive is ever passed by the package store.</returns>
    public static async Task<UnitResult<PackageError>> WriteBatchAsync(
        ImmutableArray<PrivateFileWrite> files,
        bool overwrite,
        CancellationToken token = default
    )
    {
        var planned = PrivateFilePlan.Create(files, overwrite);
        if (planned.IsFailure)
            return planned.Error;
        var entries = planned.Value;
        var result = await PackageEdge
            .Async(
                () => InstallAsync(entries, overwrite, token),
                PackageErrorCodes.FileAccessFailed,
                "The private file transaction failed."
            )
            .ConfigureAwait(false);
        var rollback = result.IsFailure
            ? ForEach(
                entries.Reverse(),
                value => value.Rollback(),
                "The transaction could not restore every original; private backups were retained."
            )
            : UnitResult.Success<PackageError>();
        var cleanup = ForEach(
            entries,
            value => value.Cleanup(result.IsSuccess),
            "The private transaction staging cleanup failed."
        );
        if (rollback.IsFailure)
            return rollback.Error;
        return cleanup.IsFailure ? cleanup.Error
            : result.IsFailure ? result.Error
            : UnitResult.Success<PackageError>();
    }

    private static UnitResult<PackageError> ForEach(
        IEnumerable<PrivateFileInstallation> entries,
        Func<PrivateFileInstallation, bool> operation,
        string message
    )
    {
        var error = Maybe<PackageError>.None;
        foreach (var entry in entries)
        {
            var result = PackageEdge.Try(
                () => operation(entry),
                PackageErrorCodes.FileAccessFailed,
                message
            );
            if (result.IsFailure && error.HasNoValue)
                error = result.Error;
            else if (result.IsSuccess && !result.Value && error.HasNoValue)
                error = new PackageError(PackageErrorCodes.FileAccessFailed, message);
        }
        return error.HasValue ? error.Value : UnitResult.Success<PackageError>();
    }

    private static async Task<Result<bool, PackageError>> InstallAsync(
        ImmutableArray<PrivateFileInstallation> entries,
        bool overwrite,
        CancellationToken token
    )
    {
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            var parent = Maybe
                .From(Path.GetDirectoryName(entry.File.Path))
                .GetValueOrDefault(string.Empty);
            PrivateFilePermissions.CreateDirectory(parent);
            await PrivateFilePermissions
                .WriteAsync(entry.Stage, entry.File.Bytes.AsMemory(), token)
                .ConfigureAwait(false);
        }
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            var installed = entry.Install(overwrite);
            if (installed.IsFailure)
                return installed.Error;
        }
        foreach (
            var parent in entries
                .Select(value =>
                    Maybe
                        .From(Path.GetDirectoryName(value.File.Path))
                        .GetValueOrDefault(string.Empty)
                )
                .Distinct()
        )
        {
            var flushed = PrivateDirectorySync.Flush(parent);
            if (flushed.IsFailure)
                return flushed.Error;
        }
        return true;
    }
}
