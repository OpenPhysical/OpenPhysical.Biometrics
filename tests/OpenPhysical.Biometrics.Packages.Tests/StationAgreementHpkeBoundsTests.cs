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

/// <summary>Checks rejected HPKE inputs and cleanup of caller-owned secret buffers.</summary>
public sealed class StationAgreementHpkeBoundsTests
{
    /// <summary>Rejects oversized spans without allocating snapshots proportional to caller input.</summary>
    [Test]
    public void OversizedSpansAreRejectedBeforeCopiesAndSecretIsCleared()
    {
        var spki = ValidSpki();
        var oversized = new byte[2 * 1024 * 1024];
        var secret = new byte[32];

        var before = GC.GetAllocatedBytesForCurrentThread();
        var sealedValue = StationAgreementHpke.SealBase(oversized, [1], [2], new byte[32]);
        var opened = StationAgreementHpke.OpenBase(spki, secret, oversized, [1], [2], [48]);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        sealedValue.Should().Fail();
        opened.Should().Fail();
        secret.Should().OnlyContain(value => value == 0);
        allocated.Should().BeLessThan(32 * 1024);
    }

    /// <summary>Checks every public span bound for both directions.</summary>
    [Test]
    public void EveryInputSpanRejectsInvalidLengths()
    {
        var spki = ValidSpki();
        var point = ValidPoint();
        var challenge = new byte[32];
        var ciphertext = new byte[48];
        var oversizedInfo = new byte[129];
        var oversizedAad = new byte[1_101];
        var invalidSeal = new[]
        {
            StationAgreementHpke.SealBase(new byte[90], [1], [2], challenge),
            StationAgreementHpke.SealBase(spki, [], [2], challenge),
            StationAgreementHpke.SealBase(spki, oversizedInfo, [2], challenge),
            StationAgreementHpke.SealBase(spki, [1], [], challenge),
            StationAgreementHpke.SealBase(spki, [1], oversizedAad, challenge),
            StationAgreementHpke.SealBase(spki, [1], [2], new byte[31]),
        };
        foreach (var result in invalidSeal)
            result.Should().Fail();

        var invalidOpen = new[]
        {
            StationAgreementHpke.OpenBase(new byte[90], new byte[32], point, [1], [2], ciphertext),
            StationAgreementHpke.OpenBase(spki, new byte[32], new byte[64], [1], [2], ciphertext),
            StationAgreementHpke.OpenBase(spki, new byte[32], point, [], [2], ciphertext),
            StationAgreementHpke.OpenBase(
                spki,
                new byte[32],
                point,
                oversizedInfo,
                [2],
                ciphertext
            ),
            StationAgreementHpke.OpenBase(spki, new byte[32], point, [1], [], ciphertext),
            StationAgreementHpke.OpenBase(spki, new byte[32], point, [1], oversizedAad, ciphertext),
            StationAgreementHpke.OpenBase(spki, new byte[32], point, [1], [2], new byte[47]),
        };
        foreach (var result in invalidOpen)
            result.Should().Fail();
    }

    /// <summary>Clears the owned ECDH bytes when an accepted-length SPKI is malformed.</summary>
    [Test]
    public void MalformedSpkiFailsWithTypedResultAndClearsSecret()
    {
        var secret = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        var opened = StationAgreementHpke.OpenBase(
            new byte[91],
            secret,
            ValidPoint(),
            [1],
            [2],
            new byte[48]
        );

        opened.Should().Fail();
        secret.Should().OnlyContain(value => value == 0);
    }

    /// <summary>Clears owned ECDH bytes for invalid secret lengths and encapsulated points.</summary>
    [Test]
    public void InvalidSecretAndPointLengthsClearOwnedSecret()
    {
        var wrongLengthSecret = Enumerable.Repeat((byte)0xA5, 31).ToArray();
        StationAgreementHpke
            .OpenBase(new byte[91], wrongLengthSecret, [], [], [], [])
            .Should()
            .Fail();
        wrongLengthSecret.Should().OnlyContain(value => value == 0);

        var secret = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        var invalidPoint = ValidPoint();
        Array.Clear(invalidPoint);
        invalidPoint[0] = 0x04;
        var opened = StationAgreementHpke.OpenBase(
            ValidSpki(),
            secret,
            invalidPoint,
            [1],
            [2],
            new byte[48]
        );

        opened.Should().Fail();
        secret.Should().OnlyContain(value => value == 0);
    }

    /// <summary>Clears the owned ECDH bytes when authentication fails during decryption.</summary>
    [Test]
    public void BadTagFailsAndClearsAgreementSecret()
    {
        var recipient = GenerateKey();
        var spki = SubjectPublicKeyInfoFactory
            .CreateSubjectPublicKeyInfo(recipient.Public)
            .GetDerEncoded();
        var sealedValue = StationAgreementHpke.SealBase(spki, [1], [2], new byte[32]);
        sealedValue.Should().Succeed();
        var secret = Agree(
            recipient.Private,
            recipient.Public,
            sealedValue.Value.Encapsulation.Span
        );
        var ciphertext = sealedValue.Value.Ciphertext.ToArray();
        ciphertext[^1] ^= 1;

        var opened = StationAgreementHpke.OpenBase(
            spki,
            secret,
            sealedValue.Value.Encapsulation.Span,
            [1],
            [2],
            ciphertext
        );

        opened.Should().Fail();
        secret.Should().OnlyContain(value => value == 0);
    }

    private static byte[] ValidSpki()
    {
        var key = GenerateKey();
        return SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(key.Public).GetDerEncoded();
    }

    private static byte[] ValidPoint()
    {
        var key = GenerateKey();
        return ((ECPublicKeyParameters)key.Public).Q.GetEncoded(false);
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
