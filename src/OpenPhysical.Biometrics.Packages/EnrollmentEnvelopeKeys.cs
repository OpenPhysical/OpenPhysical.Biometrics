// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;

/// <summary>External signing operation; digest is exactly SHA-256 of signed bytes.</summary>
public interface IEnrollmentSigner
{
    /// <summary>Gets the DER SubjectPublicKeyInfo for the P-256 signing key.</summary>
    ReadOnlyMemory<byte> SubjectPublicKeyInfo { get; }

    /// <summary>Gets SHA-256 of the DER SubjectPublicKeyInfo.</summary>
    ReadOnlyMemory<byte> KeyId { get; }

    /// <summary>Signs an already computed 32-byte SHA-256 digest.</summary>
    /// <param name="sha256Digest">Digest bytes supplied to the ECDSA primitive.</param>
    /// <returns>DER encoded ECDSA signature or a typed error.</returns>
    Result<byte[], PackageError> SignDigest(ReadOnlySpan<byte> sha256Digest);
}

/// <summary>Resolves trusted signing keys by the SHA-256 of DER SubjectPublicKeyInfo.</summary>
public interface IEnrollmentKeyResolver
{
    /// <summary>Resolves a trusted DER SubjectPublicKeyInfo.</summary>
    /// <param name="keyId">SHA-256 key fingerprint.</param>
    /// <returns>DER SubjectPublicKeyInfo or a typed trust failure.</returns>
    Result<byte[], PackageError> ResolveSubjectPublicKeyInfo(ReadOnlySpan<byte> keyId);
}

/// <summary>Recipient key identity with an opaque P-256 raw ECDH operation.</summary>
public interface IEnrollmentRecipientKey
{
    /// <summary>Gets the DER SubjectPublicKeyInfo for the P-256 ECDH key.</summary>
    ReadOnlyMemory<byte> SubjectPublicKeyInfo { get; }

    /// <summary>Gets SHA-256 of the DER SubjectPublicKeyInfo.</summary>
    ReadOnlyMemory<byte> KeyId { get; }

    /// <summary>Derives the raw 32-byte P-256 ECDH x-coordinate.</summary>
    /// <param name="ephemeralUncompressedPoint">Validated SEC1 uncompressed P-256 point.</param>
    /// <returns>Raw shared secret or a typed operation failure.</returns>
    Result<byte[], PackageError> DeriveSharedSecret(ReadOnlySpan<byte> ephemeralUncompressedPoint);
}
