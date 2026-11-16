// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;

/// <summary>Seals and opens strict DER v2 recipient envelopes without exposing partial plaintext.</summary>
public static class EnrollmentEnvelopeV2Codec
{
    /// <summary>Maximum accepted encoded envelope size, including DER framing and signatures.</summary>
    public const int MaximumEncodedEnvelopeLength = 134_217_728 + 1_500_000;

    /// <summary>Encrypts file zero's signed origin manifest and all ordered artifacts.</summary>
    /// <param name="transfer">Complete transfer metadata and ordered artifact bytes.</param>
    /// <param name="sender">Opaque envelope-signing key operation.</param>
    /// <param name="recipientSubjectPublicKeyInfo">Recipient P-256 DER SubjectPublicKeyInfo.</param>
    /// <returns>Canonical DER envelope or a typed failure.</returns>
    public static Result<byte[], PackageError> Seal(
        EnrollmentEnvelopeTransfer transfer,
        IEnrollmentSigner sender,
        ReadOnlySpan<byte> recipientSubjectPublicKeyInfo
    )
    {
        var recipientSpki = recipientSubjectPublicKeyInfo.ToArray();
        return PackageEdge.Bind(
            () => EnrollmentEnvelopeV2Seal.SealCore(transfer, sender, recipientSpki),
            PackageErrorCodes.CryptoFailed,
            "The enrollment envelope could not be sealed."
        );
    }

    /// <summary>Authenticates the sender before ECDH, then validates all plaintext before returning.</summary>
    /// <param name="encoded">One canonical DER v2 envelope.</param>
    /// <param name="resolver">Trusted sender and origin signing key resolver.</param>
    /// <param name="recipient">Opaque recipient P-256 ECDH key operation.</param>
    /// <returns>The complete validated transfer or a typed failure.</returns>
    public static Result<EnrollmentEnvelopeOpened, PackageError> Open(
        ReadOnlySpan<byte> encoded,
        IEnrollmentKeyResolver resolver,
        IEnrollmentRecipientKey recipient
    )
    {
        if (encoded.Length > MaximumEncodedEnvelopeLength)
            return EnrollmentEnvelopeV2Encoding.Limit(
                "The envelope length is outside supported limits."
            );
        var envelope = encoded.ToArray();
        if (!EnrollmentDerPreflight.IsSafe(envelope))
            return EnrollmentEnvelopeV2Encoding.Invalid(
                "The envelope DER exceeds supported structure limits."
            );
        return PackageEdge.Bind(
            () => EnrollmentEnvelopeV2Open.Open(envelope, resolver, recipient),
            PackageErrorCodes.InvalidEnvelope,
            "The enrollment envelope is malformed or unauthenticated."
        );
    }
}
