// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;

/// <summary>Authenticates a bounded portable ZIP package before exposing any unsigned artifacts.</summary>
public static class BiometricPackageCodec
{
    /// <summary>Encrypts the manifest and every artifact with fresh salt and nonce.</summary>
    /// <param name="package">Complete immutable unsigned staging package.</param>
    /// <param name="secret">Owned exact UTF-8 passphrase.</param>
    /// <param name="policy">Validated archive resource bounds.</param>
    /// <returns>A versioned Argon2id/AES-256-GCM envelope or a typed failure.</returns>
    public static Result<byte[], PackageError> Seal(
        BiometricPackage package,
        PackageSecret secret,
        PackageProtectionPolicy policy
    )
    {
        var archive = PackageArchiveWriter.Write(package, policy);
        if (archive.IsFailure)
            return archive.Error;
        try
        {
            return PackageEdge.Bind(
                () =>
                {
                    var header = PackageEnvelope.Create(archive.Value.Length);
                    return PackageCryptography
                        .Transform(archive.Value, header, secret, true)
                        .Map(ciphertext => Combine(header, ciphertext));
                },
                PackageErrorCodes.CryptoFailed,
                "The cryptographic provider could not seal the package."
            );
        }
        finally
        {
            Array.Clear(archive.Value);
        }
    }

    /// <summary>Validates public resource bounds, authenticates the entire envelope, then validates ZIP and manifest members.</summary>
    /// <param name="envelope">Exact protected package bytes.</param>
    /// <param name="secret">Owned passphrase; a wrong value releases no plaintext.</param>
    /// <param name="policy">Validated archive resource bounds.</param>
    /// <returns>The complete authenticated package, or a failure with no exposed artifact collection.</returns>
    public static Result<BiometricPackage, PackageError> Open(
        ReadOnlySpan<byte> envelope,
        PackageSecret secret,
        PackageProtectionPolicy policy
    )
    {
        var valid = PackageEnvelope.Validate(envelope, policy);
        if (valid.IsFailure)
            return valid.Error;
        var plaintext = PackageCryptography.Transform(
            envelope[PackageEnvelope.HeaderLength..].ToArray(),
            envelope[..PackageEnvelope.HeaderLength].ToArray(),
            secret,
            false
        );
        if (plaintext.IsFailure)
            return plaintext.Error;
        try
        {
            return PackageArchiveReader.Read(plaintext.Value, policy);
        }
        finally
        {
            Array.Clear(plaintext.Value);
        }
    }

    private static byte[] Combine(byte[] header, byte[] ciphertext)
    {
        var result = new byte[header.Length + ciphertext.Length];
        header.CopyTo(result, 0);
        ciphertext.CopyTo(result, header.Length);
        return result;
    }
}
