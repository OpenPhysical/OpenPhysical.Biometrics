// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using AwesomeAssertions;
using OpenPhysical.Biometrics.Piv;
using OpenPhysical.Middleware.Domain.Cryptography.Oids;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Piv.Codec;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.CredentialCache;
using OpenPhysical.Middleware.Infrastructure.Validation;
using OpenPhysical.Middleware.Piv.Codec;
using OpenPhysical.Middleware.Validation;
using OpenPhysical.Testing.Piv;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.X509;

namespace OpenPhysical.Middleware.Tests;

internal sealed record TwicReferenceTestData(
    FingerprintReferenceCertificates Certificates,
    AsymmetricCipherKeyPair OperatorKeys,
    X509Certificate OperatorCertificate,
    byte[] FascN,
    ImmutableArray<byte> Chuid,
    ImmutableArray<byte> Encrypted,
    ImmutableArray<byte> Key
)
{
    internal static async Task<TwicReferenceTestData> CreateAsync(
        TwicCardModel generation,
        bool attended
    )
    {
        var fascn = new FascN(9998, 1, 12345);
        var bytes = FascNCodec.EncodeToBytes(fascn).Value;
        var identity = Identity(generation, bytes);
        var certificates = FingerprintReferenceCertificates.Create(bytes, twic: true);
        var operatorKeys = VerificationBundleFixture.NewSigner().Keys;
        var operatorCertificate = OperatorCertificateFor(operatorKeys, certificates);
        var chuid = FingerprintReferenceTestData.SignedChuid(
            fascn,
            identity.CardUuid,
            certificates
        );
        AssertChuidAndPaths(certificates, chuid);
        var biometric = await TwicBiometricTestData.CreateAsync(
            identity,
            certificates,
            generation,
            attended
        );
        var key = Enumerable.Range(1, 16).Select(value => (byte)value).ToImmutableArray();
        return new(
            certificates,
            operatorKeys,
            operatorCertificate,
            bytes,
            chuid,
            PivTlvWriter.Write(0xbc, Encrypt(key.ToArray(), biometric)),
            key
        );
    }

    private static BiometricIdentity Identity(TwicCardModel generation, byte[] fascn) =>
        generation == TwicCardModel.Legacy
            ? BiometricIdentity.CreateWithoutCardUuid(fascn).Value
            : BiometricIdentity
                .Create(fascn, Guid.Parse("909d0ec3-2e77-5a4e-b863-2a3e4352bf48"))
                .Value;

    private static X509Certificate OperatorCertificateFor(
        AsymmetricCipherKeyPair keys,
        FingerprintReferenceCertificates certificates
    ) =>
        CeremonyCertificateFixture.PinCertificate(
            keys.Public,
            certificates.RootKeys.Private,
            false,
            leafSerial: 4
        );

    private static void AssertChuidAndPaths(
        FingerprintReferenceCertificates certificates,
        ImmutableArray<byte> chuid
    )
    {
        var extracted = ChuidCodec.ExtractSignedContent(chuid);
        extracted.IsSuccess.Should().BeTrue("fixture CHUID extraction");
        var checkedChuid = SignedContent.VerifyBytes(
            extracted.Value.UnsignedBytes,
            extracted.Value.SignatureBytes,
            certificates.Content,
            KnownOids.Piv.ChuidSecurityObject.DerObjectIdentifier
        );
        checkedChuid
            .IsSuccess.Should()
            .BeTrue(checkedChuid.IsFailure ? checkedChuid.Error : "fixture CHUID signature");
        var policy = TrustedCertificateService.EvaluateContentSigningPolicy(
            certificates.Content,
            CertificateOidPolicyProfiles.TwicContentSigningProfileId
        );
        (policy.IsSuccess && policy.Value.IsCompliant)
            .Should()
            .BeTrue("fixture TWIC content policy");
        foreach (var leaf in new[] { certificates.Authentication, certificates.Content })
        {
            var path = CertificatePathValidationService.ValidateSuppliedPath(
                [leaf, certificates.Root],
                [certificates.Root],
                VerificationBundleFixture.Now
            );
            path.State.Should()
                .Be(VerificationCheckState.Passed, string.Join(",", path.ReasonCodes));
        }
    }

    private static byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        var cipher = new PaddedBufferedBlockCipher(
            new EcbBlockCipher(new AesEngine()),
            new Pkcs7Padding()
        );
        cipher.Init(true, new KeyParameter(key));
        return cipher.DoFinal(plaintext);
    }
}
