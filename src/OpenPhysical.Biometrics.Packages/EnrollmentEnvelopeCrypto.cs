// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;

internal static class EnrollmentEnvelopeCrypto
{
    private static readonly byte[] HpkeVersion = "HPKE-v1"u8.ToArray();
    private static readonly byte[] KemSuite = [.. "KEM"u8, 0x00, 0x10];
    private static readonly byte[] HpkeSuite = [.. "HPKE"u8, 0x00, 0x10, 0x00, 0x01, 0x00, 0x02];

    internal static (byte[] Encapsulation, byte[] Ciphertext) Seal(
        ReadOnlySpan<byte> recipientSpki,
        byte[] info,
        byte[] aad,
        byte[] plaintext
    )
    {
        var recipient =
            PublicKeyFactory.CreateKey(recipientSpki.ToArray()) as ECPublicKeyParameters
            ?? throw new ArgumentException("Recipient key must be P-256.");
        EnrollmentHpkeSchedule.ValidatePublic(recipient.Q, recipient.Parameters);
        EnrollmentHpkeSchedule.RequireCurve(recipient.Parameters);
        var generator = new ECKeyPairGenerator("ECDH");
        generator.Init(new ECKeyGenerationParameters(recipient.Parameters, new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        var ephemeral = (ECPublicKeyParameters)pair.Public;
        var agreement = new ECDHBasicAgreement();
        agreement.Init(pair.Private);
        var dh = BigIntegers.AsUnsignedByteArray(32, agreement.CalculateAgreement(recipient));
        try
        {
            var enc = ephemeral.Q.GetEncoded(false);
            return (
                enc,
                EnrollmentHpkeSchedule.Encrypt(
                    dh,
                    enc,
                    recipient.Q.GetEncoded(false),
                    info,
                    aad,
                    plaintext
                )
            );
        }
        finally
        {
            Array.Clear(dh);
        }
    }

    internal static byte[] Open(
        IEnrollmentRecipientKey recipient,
        byte[] enc,
        byte[] info,
        byte[] aad,
        byte[] ciphertext
    )
    {
        var publicKey =
            PublicKeyFactory.CreateKey(recipient.SubjectPublicKeyInfo.ToArray())
                as ECPublicKeyParameters
            ?? throw new ArgumentException("Recipient key must be P-256.");
        EnrollmentHpkeSchedule.RequireCurve(publicKey.Parameters);
        var point = publicKey.Parameters.Curve.DecodePoint(enc);
        EnrollmentHpkeSchedule.ValidatePublic(point, publicKey.Parameters);
        var shared = recipient.DeriveSharedSecret(enc);
        if (shared.IsFailure)
            throw new InvalidOperationException("Recipient ECDH operation failed.");
        try
        {
            if (shared.Value.Length != 32)
                throw new InvalidOperationException("Recipient ECDH returned an invalid secret.");
            return EnrollmentHpkeSchedule.Decrypt(
                shared.Value,
                enc,
                publicKey.Q.GetEncoded(false),
                info,
                aad,
                ciphertext
            );
        }
        finally
        {
            Array.Clear(shared.Value);
        }
    }

    internal static bool Verify(
        ReadOnlySpan<byte> spki,
        ReadOnlySpan<byte> digest,
        ReadOnlySpan<byte> signature
    )
    {
        var key = PublicKeyFactory.CreateKey(spki.ToArray()) as ECPublicKeyParameters;
        if (key is null || digest.Length != 32 || signature.Length is < 8 or > 80)
            return false;
        EnrollmentHpkeSchedule.RequireCurve(key.Parameters);
        EnrollmentHpkeSchedule.ValidatePublic(key.Q, key.Parameters);
        var parsed = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(signature.ToArray()));
        if (
            parsed.Count != 2
            || !parsed.GetEncoded(Asn1Encodable.Der).AsSpan().SequenceEqual(signature)
        )
            return false;
        var r = DerInteger.GetInstance(parsed[0]).Value;
        var s = DerInteger.GetInstance(parsed[1]).Value;
        if (!ValidScalar(r, key.Parameters.N) || !ValidScalar(s, key.Parameters.N))
            return false;
        var verifier = new ECDsaSigner();
        verifier.Init(false, key);
        return verifier.VerifySignature(digest.ToArray(), r, s);
    }

    private static bool ValidScalar(
        Org.BouncyCastle.Math.BigInteger value,
        Org.BouncyCastle.Math.BigInteger order
    ) => value.SignValue > 0 && value.CompareTo(order) < 0;
}
