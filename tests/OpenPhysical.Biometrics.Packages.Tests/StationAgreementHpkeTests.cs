// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

/// <summary>Checks the bounded challenge HPKE wrapper independently from package envelopes.</summary>
public sealed class StationAgreementHpkeTests
{
    /// <summary>Opens a valid challenge and confirms the caller-owned ECDH bytes are cleared.</summary>
    [Test]
    public void SingleShotChallengeOpensWithOpaqueAgreementSecretAndClearsIt()
    {
        var recipient = GenerateKey();
        var spki = SubjectPublicKeyInfoFactory
            .CreateSubjectPublicKeyInfo(recipient.Public)
            .GetDerEncoded();
        spki.Length.Should().Be(91);
        var challenge = Enumerable.Range(0, 32).Select(index => (byte)index).ToArray();
        var info = "OpenPhysical/StationAgreementPoP/v1/info\0fixture"u8.ToArray();
        var aad = "OpenPhysical/StationAgreementPoP/v1/aad\0statement"u8.ToArray();
        var sealedValue = StationAgreementHpke.SealBase(spki, info, aad, challenge);

        sealedValue.Should().Succeed();
        sealedValue.Value.Encapsulation.Length.Should().Be(65);
        sealedValue.Value.Ciphertext.Length.Should().Be(48);

        var secret = Agree(
            recipient.Private,
            recipient.Public,
            sealedValue.Value.Encapsulation.Span
        );
        var opened = StationAgreementHpke.OpenBase(
            spki,
            secret,
            sealedValue.Value.Encapsulation.Span,
            info,
            aad,
            sealedValue.Value.Ciphertext.Span
        );

        opened.Should().Succeed();
        opened.Value.Should().Equal(challenge);
        secret.Should().OnlyContain(value => value == 0);
    }

    /// <summary>Rejects authenticated-data substitution and clears ECDH bytes on failure.</summary>
    [Test]
    public void ChangedAssociatedDataFailsAndClearsAgreementSecret()
    {
        var recipient = GenerateKey();
        var spki = SubjectPublicKeyInfoFactory
            .CreateSubjectPublicKeyInfo(recipient.Public)
            .GetDerEncoded();
        spki.Length.Should().Be(91);
        var challenge = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        var sealedValue = StationAgreementHpke.SealBase(spki, [1], [2], challenge);
        sealedValue.Should().Succeed();
        var secret = Agree(
            recipient.Private,
            recipient.Public,
            sealedValue.Value.Encapsulation.Span
        );

        var opened = StationAgreementHpke.OpenBase(
            spki,
            secret,
            sealedValue.Value.Encapsulation.Span,
            [1],
            [3],
            sealedValue.Value.Ciphertext.Span
        );

        opened.Should().Fail();
        opened.Error.Code.Should().Be(PackageErrorCodes.AuthenticationFailed);
        secret.Should().OnlyContain(value => value == 0);
    }

    /// <summary>Rejects a challenge with a nonprotocol plaintext length.</summary>
    [Test]
    public void InvalidLengthsFailBeforeAnyCiphertextIsReturned()
    {
        var recipient = GenerateKey();
        var spki = SubjectPublicKeyInfoFactory
            .CreateSubjectPublicKeyInfo(recipient.Public)
            .GetDerEncoded();
        var sealedValue = StationAgreementHpke.SealBase(spki, [1], [2], new byte[31]);

        sealedValue.Should().Fail();
        sealedValue.Error.Code.Should().Be(PackageErrorCodes.InvalidEnvelope);
    }

    private static AsymmetricCipherKeyPair GenerateKey()
    {
        var curve = ECNamedCurveTable.GetByName("secp256r1");
        var baseDomain = new ECDomainParameters(
            curve.Curve,
            curve.G,
            curve.N,
            curve.H,
            curve.GetSeed()
        );
        var domain = new ECNamedDomainParameters(X9ObjectIdentifiers.Prime256v1, baseDomain);
        var generator = new ECKeyPairGenerator("ECDH");
        generator.Init(new ECKeyGenerationParameters(domain, new SecureRandom()));
        return generator.GenerateKeyPair();
    }

    private static byte[] Agree(
        AsymmetricKeyParameter privateKey,
        AsymmetricKeyParameter recipientPublicKey,
        ReadOnlySpan<byte> encapsulation
    )
    {
        var publicKey = (ECPublicKeyParameters)recipientPublicKey;
        var peer = new ECPublicKeyParameters(
            publicKey.Parameters.Curve.DecodePoint(encapsulation.ToArray()),
            publicKey.Parameters
        );
        var agreement = new ECDHBasicAgreement();
        agreement.Init(privateKey);
        return Org.BouncyCastle.Utilities.BigIntegers.AsUnsignedByteArray(
            32,
            agreement.CalculateAgreement(peer)
        );
    }
}
