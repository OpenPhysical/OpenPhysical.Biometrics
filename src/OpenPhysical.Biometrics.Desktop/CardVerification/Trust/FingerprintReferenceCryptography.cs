// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Piv;
using OpenPhysical.CardEdge.Credential;
using OpenPhysical.CardEdge.Twic.Crypto;
using OpenPhysical.Middleware.Cbeff.Signatures;
using OpenPhysical.Middleware.Domain.Cryptography.Oids;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Piv.Codec;
using OpenPhysical.Middleware.Domain.SmartCard;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.CredentialCache;
using OpenPhysical.Middleware.Infrastructure.Inspection;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Middleware.Infrastructure.Validation;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Piv.Codec;
using OpenPhysical.Middleware.Validation;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.X509;
using BiometricValidation = OpenPhysical.Biometrics.Piv.ValidationStatus;
using CbeffCodec = OpenPhysical.Biometrics.Formats.Cbeff.CbeffCodec;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

internal static class FingerprintReferenceCryptography
{
    internal static Result<ImmutableArray<byte>, VerificationError> DecryptTwicReference(
        ImmutableArray<byte> encrypted,
        TwicPrivacyKeyContainer privacyKey
    )
    {
        if (!privacyKey.IsAesEcb || privacyKey.Key.Length != 16 || encrypted.IsDefaultOrEmpty)
            return Result.Failure<ImmutableArray<byte>, VerificationError>(
                new(VerificationErrorCodes.EvidenceUnavailable, "ceremony.reference-unavailable")
            );
        var objects = PivTlvReader.ReadAll(encrypted.ToArray());
        if (objects.Count != 1 || objects[0].Tag.Length != 1 || objects[0].Tag[0] != 0xbc)
            return Result.Failure<ImmutableArray<byte>, VerificationError>(
                new(VerificationErrorCodes.EvidenceUnavailable, "ceremony.reference-unavailable")
            );
        var plaintext = TwicPrivacyKeyCipher.Decrypt(privacyKey, objects[0].Value);
        return plaintext.IsSuccess
            ? plaintext.Value
            : Result.Failure<ImmutableArray<byte>, VerificationError>(
                new(VerificationErrorCodes.EvidenceUnavailable, "ceremony.reference-unavailable")
            );
    }

    internal static void Clear(ImmutableArray<byte> bytes)
    {
        var owned = System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsArray(bytes);
        if (owned is not null)
            Array.Clear(owned);
    }

    internal static string Hash(byte[] bytes) =>
        Convert.ToHexStringLower(OpenPhysical.Directory.Contracts.DirectoryDigest.Sha256(bytes));
}
