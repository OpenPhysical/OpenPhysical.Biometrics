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
internal sealed class EnrollmentHpkeAes256VectorTests
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
    public void P256Aes256BaseModeMatchesCfrgHpkeVector()
    {
        var ephemeralPrivate = Hex(
            "90345e3a1d116c1dd39ae76d95ab858c142223a63e44f8f85318cfa91a84858e"
        );
        var encapsulation = Hex(
            "04c06b4f6bebc7bb495cb797ab753f911aff80aefb86fd8b6fcc35525f3ab5f03e"
                + "0b21bd31a86c6048af3cb2d98e0d3bf01da5cc4c39ff5370d331a4f1f7d5a4e0"
        );
        var recipientPublic = Hex(
            "04abc7e49a4c6b3566d77d0304addc6ed0e98512ffccf505e6a8e3eb25c685136f"
                + "853148544876de76c0f2ef99cdc3a05ccf5ded7860c7c021238f9e2073d2356c"
        );
        AssertSharedSecret(ephemeralPrivate, encapsulation, recipientPublic);
        var info = Hex("4f6465206f6e2061204772656369616e2055726e");
        AssertKeySchedule(ephemeralPrivate, encapsulation, recipientPublic, info);
        AssertCiphertext(ephemeralPrivate, encapsulation, recipientPublic, info);
    }

    private static void AssertSharedSecret(
        byte[] privateKey,
        byte[] encapsulation,
        byte[] recipientPublic
    ) =>
        KemSharedSecret(privateKey, encapsulation, recipientPublic)
            .Should()
            .Equal(Hex("48893fecd82f7c3456af6a42d8f56325d21e08c10fa81299986aaff54cde7b49"));

    private static void AssertKeySchedule(
        byte[] privateKey,
        byte[] encapsulation,
        byte[] recipientPublic,
        byte[] info
    )
    {
        var (key, nonce) = EnrollmentHpkeSchedule.VectorKeySchedule(
            KemSharedSecret(privateKey, encapsulation, recipientPublic),
            info,
            0x02,
            32
        );
        key.Should().Equal(Hex("ee16802a936d5f544771131900ee6973d0551de9e852ece2ef34bf0d5f9e1d1d"));
        nonce.Should().Equal(Hex("9bc50980832a7b4b58c40161"));
    }

    private static void AssertCiphertext(
        byte[] privateKey,
        byte[] encapsulation,
        byte[] recipientPublic,
        byte[] info
    ) =>
        EnrollmentHpkeSchedule
            .VectorSeal(
                KemSharedSecret(privateKey, encapsulation, recipientPublic),
                info,
                Hex("436f756e742d30"),
                Hex("4265617574792069732074727574682c20747275746820626561757479"),
                0x02,
                32
            )
            .Should()
            .Equal(
                Hex(
                    "58c61a45059d0c5704560e9d88b564a8b63f1364b8d1fcb3c4c6ddc1d291742465e902cd216f8908da49f8f96f"
                )
            );

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
