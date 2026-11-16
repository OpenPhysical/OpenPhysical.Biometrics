// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

/// <summary>Bounded single-shot HPKE operations for station agreement-key possession proofs.</summary>
public static class StationAgreementHpke
{
    private const int ChallengeLength = 32;
    private const int EncapsulationLength = 65;
    private const int CiphertextLength = 48;
    private const int SpkiLength = 91;
    private const int MaximumInfoLength = 128;
    private const int MaximumAadLength = 1_100;

    /// <summary>Encrypts exactly one 32-byte challenge using the fixed P-256/HKDF-SHA256/AES-256-GCM suite.</summary>
    /// <param name="recipientSubjectPublicKeyInfo">Canonical DER P-256 SubjectPublicKeyInfo.</param>
    /// <param name="info">Bounded application-specific HPKE info.</param>
    /// <param name="aad">Bounded application-specific authenticated data.</param>
    /// <param name="challenge">Exactly 32 unpredictable challenge bytes.</param>
    /// <returns>The ephemeral point and 48-byte authenticated ciphertext.</returns>
    public static Result<StationAgreementHpkeCiphertext, PackageError> SealBase(
        ReadOnlySpan<byte> recipientSubjectPublicKeyInfo,
        ReadOnlySpan<byte> info,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> challenge
    )
    {
        if (!ValidLengths(recipientSubjectPublicKeyInfo, info, aad, challenge))
            return InvalidInputs();

        var inputs = new byte[4][];
        try
        {
            inputs[0] = recipientSubjectPublicKeyInfo.ToArray();
            inputs[1] = info.ToArray();
            inputs[2] = aad.ToArray();
            inputs[3] = challenge.ToArray();
            var validKey = PackageEdge.Try(
                () => ValidPublicKey(inputs[0]),
                PackageErrorCodes.InvalidEnvelope,
                "The station challenge inputs are invalid."
            );
            if (validKey.IsFailure || !validKey.Value)
                return InvalidInputs();

            return PackageEdge.Try(
                () => SealCore(inputs),
                PackageErrorCodes.CryptoFailed,
                "The station challenge could not be encrypted."
            );
        }
        finally
        {
            Clear(inputs);
        }
    }

    /// <summary>Opens exactly one challenge after opaque custody has produced its transient P-256 ECDH result.</summary>
    /// <param name="recipientSubjectPublicKeyInfo">Canonical DER P-256 SubjectPublicKeyInfo.</param>
    /// <param name="agreementSecret">Owned transient 32-byte P-256 ECDH result; cleared by this call.</param>
    /// <param name="encapsulation">The 65-byte uncompressed P-256 ephemeral point.</param>
    /// <param name="info">Bounded application-specific HPKE info.</param>
    /// <param name="aad">Bounded application-specific authenticated data.</param>
    /// <param name="ciphertext">Exactly 48 ciphertext and authentication-tag bytes.</param>
    /// <returns>The 32-byte challenge or a typed error.</returns>
    public static Result<byte[], PackageError> OpenBase(
        ReadOnlySpan<byte> recipientSubjectPublicKeyInfo,
        byte[] agreementSecret,
        ReadOnlySpan<byte> encapsulation,
        ReadOnlySpan<byte> info,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> ciphertext
    )
    {
        if (agreementSecret is null)
            return InvalidOpen();
        if (agreementSecret.Length != 32)
        {
            Array.Clear(agreementSecret);
            return InvalidOpen();
        }

        var inputs = new byte[5][];
        try
        {
            if (
                !ValidOpenLengths(
                    recipientSubjectPublicKeyInfo,
                    encapsulation,
                    info,
                    aad,
                    ciphertext
                )
            )
                return InvalidOpen();

            inputs[0] = recipientSubjectPublicKeyInfo.ToArray();
            inputs[1] = encapsulation.ToArray();
            inputs[2] = info.ToArray();
            inputs[3] = aad.ToArray();
            inputs[4] = ciphertext.ToArray();
            var validKey = PackageEdge.Try(
                () => ValidPublicKey(inputs[0]),
                PackageErrorCodes.InvalidEnvelope,
                "The station challenge inputs are invalid."
            );
            if (validKey.IsFailure || !validKey.Value)
                return InvalidOpen();

            return PackageEdge.Bind(
                () => OpenCore(agreementSecret, inputs),
                PackageErrorCodes.AuthenticationFailed,
                "The station challenge is invalid or has been changed."
            );
        }
        finally
        {
            Array.Clear(agreementSecret);
            Clear(inputs);
        }
    }

    private static bool ValidLengths(
        ReadOnlySpan<byte> spki,
        ReadOnlySpan<byte> info,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> challenge
    ) =>
        spki.Length == SpkiLength
        && info.Length is > 0 and <= MaximumInfoLength
        && aad.Length is > 0 and <= MaximumAadLength
        && challenge.Length == ChallengeLength;

    private static bool ValidOpenLengths(
        ReadOnlySpan<byte> spki,
        ReadOnlySpan<byte> encapsulation,
        ReadOnlySpan<byte> info,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> ciphertext
    ) =>
        spki.Length == SpkiLength
        && encapsulation.Length == EncapsulationLength
        && encapsulation[0] == 0x04
        && info.Length is > 0 and <= MaximumInfoLength
        && aad.Length is > 0 and <= MaximumAadLength
        && ciphertext.Length == CiphertextLength;

    private static StationAgreementHpkeCiphertext SealCore(byte[][] input)
    {
        var result = EnrollmentEnvelopeCrypto.Seal(input[0], input[1], input[2], input[3]);
        return new StationAgreementHpkeCiphertext(result.Encapsulation, result.Ciphertext);
    }

    private static Result<byte[], PackageError> OpenCore(byte[] secret, byte[][] input)
    {
        var recipient = (ECPublicKeyParameters)PublicKeyFactory.CreateKey(input[0]);
        var point = recipient.Parameters.Curve.DecodePoint(input[1]);
        EnrollmentHpkeSchedule.RequireCurve(recipient.Parameters);
        EnrollmentHpkeSchedule.ValidatePublic(point, recipient.Parameters);
        var plaintext = EnrollmentHpkeSchedule.Decrypt(
            secret,
            input[1],
            recipient.Q.GetEncoded(false),
            input[2],
            input[3],
            input[4]
        );
        if (plaintext.Length == ChallengeLength)
            return plaintext;

        Array.Clear(plaintext);
        return InvalidOpen();
    }

    private static bool ValidPublicKey(ReadOnlySpan<byte> spki)
    {
        if (spki.Length != SpkiLength)
            return false;

        var parsed = PublicKeyFactory.CreateKey(spki.ToArray()) as ECPublicKeyParameters;
        if (parsed is null)
            return false;

        EnrollmentHpkeSchedule.RequireCurve(parsed.Parameters);
        EnrollmentHpkeSchedule.ValidatePublic(parsed.Q, parsed.Parameters);
        var canonicalSpki = SubjectPublicKeyInfoFactory
            .CreateSubjectPublicKeyInfo(parsed)
            .GetDerEncoded();
        return parsed.Q.GetEncoded(false).Length == EncapsulationLength
            && canonicalSpki.AsSpan().SequenceEqual(spki);
    }

    private static Result<StationAgreementHpkeCiphertext, PackageError> InvalidInputs() =>
        new PackageError(
            PackageErrorCodes.InvalidEnvelope,
            "The station challenge inputs are invalid."
        );

    private static Result<byte[], PackageError> InvalidOpen() =>
        new PackageError(
            PackageErrorCodes.InvalidEnvelope,
            "The station challenge inputs are invalid."
        );

    private static void Clear(params byte[][] buffers)
    {
        foreach (var buffer in buffers)
        {
            if (buffer is not null)
                Array.Clear(buffer);
        }
    }
}
