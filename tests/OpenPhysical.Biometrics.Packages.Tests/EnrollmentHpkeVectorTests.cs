// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Utilities;

[TestFixture]
internal sealed class EnrollmentHpkeVectorTests
{
    private static readonly X9ECParameters Curve = ECNamedCurveTable.GetByName("secp256r1");
    private static readonly ECDomainParameters Domain = new(
        Curve.Curve,
        Curve.G,
        Curve.N,
        Curve.H,
        Curve.GetSeed()
    );

    [Test]
    public void P256BaseKemKeyScheduleAndAes128MatchRfc9180AppendixA3()
    {
        var ephemeralPrivate = Hex(
            "4995788ef4b9d6132b249ce59a77281493eb39af373d236a1fe415cb0c2d7beb"
        );
        var encapsulation = Hex(
            "04a92719c6195d5085104f469a8b9814d5838ff72b60501e2c4466e5e67b325"
                + "ac98536d7b61a1af4b78e5b7f951c0900be863c403ce65c9bfcb9382657222d18c4"
        );
        var recipientPublic = Hex(
            "04fe8c19ce0905191ebc298a9245792531f26f0cece2460639e8bc39cb7f70"
                + "6a826a779b4cf969b8a0e539c7f62fb3d30ad6aa8f80e30f1d128aafd68a2ce72ea0"
        );
        var shared = KemSharedSecret(ephemeralPrivate, encapsulation, recipientPublic);
        shared
            .Should()
            .Equal(Hex("c0d26aeab536609a572b07695d933b589dcf363ff9d93c93adea537aeabb8cb8"));

        var info = Hex("4f6465206f6e2061204772656369616e2055726e");
        var (key, nonce) = EnrollmentHpkeSchedule.VectorKeySchedule(shared, info, 0x01, 16);
        key.Should().Equal(Hex("868c066ef58aae6dc589b6cfdd18f97e"));
        nonce.Should().Equal(Hex("4e0bc5018beba4bf004cca59"));
        var ciphertext = EnrollmentHpkeSchedule.VectorSeal(
            KemSharedSecret(ephemeralPrivate, encapsulation, recipientPublic),
            info,
            Hex("436f756e742d30"),
            Hex("4265617574792069732074727574682c20747275746820626561757479"),
            0x01,
            16
        );
        ciphertext
            .Should()
            .Equal(
                Hex(
                    "5ad590bb8baa577f8619db35a36311226a896e7342a6d836d8b7bcd2f20b6c7f"
                        + "9076ac232e3ab2523f39513434"
                )
            );
    }

    [Test]
    public void SuiteTwoSingleShotRoundTripUsesRecipientEcdh()
    {
        var recipient = new EnrollmentEnvelopeTestKeys();
        var plaintext = "opaque transfer file"u8.ToArray();
        var info = "OpenPhysical.test.info"u8.ToArray();
        var aad = "OpenPhysical.test.aad"u8.ToArray();
        var sealedValue = EnrollmentEnvelopeCrypto.Seal(
            recipient.SubjectPublicKeyInfo.Span,
            info,
            aad,
            plaintext
        );
        EnrollmentEnvelopeCrypto
            .Open(recipient, sealedValue.Encapsulation, info, aad, sealedValue.Ciphertext)
            .Should()
            .Equal(plaintext);
    }

    private static byte[] KemSharedSecret(byte[] skE, byte[] enc, byte[] pkR)
    {
        var privateKey = new ECPrivateKeyParameters("ECDH", new BigInteger(1, skE), Domain);
        var publicKey = new ECPublicKeyParameters("ECDH", Domain.Curve.DecodePoint(pkR), Domain);
        var agreement = new ECDHBasicAgreement();
        agreement.Init(privateKey);
        var dh = BigIntegers.AsUnsignedByteArray(32, agreement.CalculateAgreement(publicKey));
        return EnrollmentHpkeSchedule.KemSharedSecret(dh, enc, pkR);
    }

    private static byte[] Hex(string value) => Convert.FromHexString(value);
}
