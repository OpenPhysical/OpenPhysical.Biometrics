// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Reads protected packages and installs opaque ciphertext files with shared private transactions.</summary>
public static class PackageFileStore
{
    /// <summary>Encrypts in memory and saves one new opaque package file; no plaintext archive is staged.</summary>
    /// <param name="folder">Private destination folder outside repositories and linked paths.</param>
    /// <param name="package">Complete unsigned person package.</param>
    /// <param name="secret">Owned passphrase.</param>
    /// <param name="policy">Validated resource policy.</param>
    /// <param name="token">Cancellation; key derivation is drained before returning.</param>
    /// <returns>The new opaque package path or a typed failure.</returns>
    public static Task<Result<string, PackageError>> SaveAsync(
        string folder,
        BiometricPackage package,
        PackageSecret secret,
        PackageProtectionPolicy policy,
        CancellationToken token = default
    ) =>
        PackageEdge.Async<string>(
            async () =>
            {
                var checkedFolder = PrivatePath.Validate(folder);
                if (checkedFolder.IsFailure)
                    return checkedFolder.Error;
                var encoded = await Task.Run(
                        () => BiometricPackageCodec.Seal(package, secret, policy),
                        token
                    )
                    .ConfigureAwait(false);
                if (encoded.IsFailure)
                    return encoded.Error;
                var path = Path.Combine(
                    checkedFolder.Value,
                    Guid.NewGuid().ToString("N") + ".opbio"
                );
                var saved = await PrivateFileTransaction
                    .WriteBatchAsync(
                        [new(path, ImmutableArray.Create(encoded.Value))],
                        false,
                        token
                    )
                    .ConfigureAwait(false);
                return saved.IsFailure ? saved.Error : path;
            },
            PackageErrorCodes.FileAccessFailed,
            "The protected package could not be saved."
        );

    /// <summary>Bounds file input before loading, then authenticates every byte before returning artifacts.</summary>
    /// <param name="path">Protected package file outside linked paths and repositories.</param>
    /// <param name="secret">Owned passphrase.</param>
    /// <param name="policy">Validated resource bounds.</param>
    /// <param name="token">Cancellation; cryptographic work is drained before returning.</param>
    /// <returns>The complete authenticated package or a typed failure.</returns>
    public static Task<Result<BiometricPackage, PackageError>> OpenAsync(
        string path,
        PackageSecret secret,
        PackageProtectionPolicy policy,
        CancellationToken token = default
    ) =>
        PackageEdge.Async(
            async () =>
            {
                var read = await ReadAsync(path, policy, token).ConfigureAwait(false);
                if (read.IsFailure)
                    return read.Error;
                token.ThrowIfCancellationRequested();
                var result = await Task.Run(
                        () => BiometricPackageCodec.Open(read.Value, secret, policy),
                        token
                    )
                    .ConfigureAwait(false);
                return token.IsCancellationRequested
                    ? new PackageError(PackageErrorCodes.Cancelled, "The operation was cancelled.")
                    : result;
            },
            PackageErrorCodes.FileAccessFailed,
            "The protected package could not be opened."
        );

    private static async Task<Result<byte[], PackageError>> ReadAsync(
        string path,
        PackageProtectionPolicy policy,
        CancellationToken token
    )
    {
        var checkedPath = PrivatePath.Validate(path);
        if (checkedPath.IsFailure)
            return checkedPath.Error;
        await using var stream = new FileStream(
            checkedPath.Value,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        if (stream.Length < PackageEnvelope.HeaderLength + PackageEnvelope.TagLength)
            return new PackageError(
                PackageErrorCodes.InvalidEnvelope,
                "The protected file is truncated."
            );
        if (
            stream.Length
            > (long)policy.MaximumPlaintextBytes
                + PackageEnvelope.HeaderLength
                + PackageEnvelope.TagLength
        )
            return new PackageError(
                PackageErrorCodes.LimitExceeded,
                "The protected file exceeds its input bounds."
            );
        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        if (stream.ReadByte() != -1)
            return new PackageError(
                PackageErrorCodes.InvalidEnvelope,
                "The protected file changed while it was read."
            );
        return bytes;
    }
}
