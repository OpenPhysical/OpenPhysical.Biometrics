// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

public static partial class StationAgreementHpke
{
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
        if (!ValidOwnedSecret(agreementSecret))
            return InvalidOpen();

        var inputs = Array.Empty<byte[]>();
        try
        {
            if (
                !TryCopyOpenInputs(
                    recipientSubjectPublicKeyInfo,
                    encapsulation,
                    info,
                    aad,
                    ciphertext,
                    out inputs
                )
            )
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

    private static bool ValidOwnedSecret(byte[]? agreementSecret)
    {
        if (agreementSecret is null)
            return false;
        if (agreementSecret.Length == 32)
            return true;

        Array.Clear(agreementSecret);
        return false;
    }

    private static bool TryCopyOpenInputs(
        ReadOnlySpan<byte> recipientSubjectPublicKeyInfo,
        ReadOnlySpan<byte> encapsulation,
        ReadOnlySpan<byte> info,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> ciphertext,
        out byte[][] inputs
    )
    {
        if (!ValidOpenLengths(recipientSubjectPublicKeyInfo, encapsulation, info, aad, ciphertext))
        {
            inputs = [];
            return false;
        }

        inputs = new byte[5][];
        byte[][] copiedInputs = inputs;
        copiedInputs[0] = recipientSubjectPublicKeyInfo.ToArray();
        copiedInputs[1] = encapsulation.ToArray();
        copiedInputs[2] = info.ToArray();
        copiedInputs[3] = aad.ToArray();
        copiedInputs[4] = ciphertext.ToArray();
        var validKey = PackageEdge.Try(
            () => ValidPublicKey(copiedInputs[0]),
            PackageErrorCodes.InvalidEnvelope,
            "The station challenge inputs are invalid."
        );
        return !validKey.IsFailure && validKey.Value;
    }

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
}
