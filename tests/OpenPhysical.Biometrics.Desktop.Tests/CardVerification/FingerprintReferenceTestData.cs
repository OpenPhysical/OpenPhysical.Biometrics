// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using AwesomeAssertions;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Piv;
using OpenPhysical.Middleware.Cbeff.Signatures;
using OpenPhysical.Middleware.Domain.Cryptography.Oids;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Piv.Codec;
using OpenPhysical.Middleware.Piv.Codec;
using OpenPhysical.Testing.Piv;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace OpenPhysical.Middleware.Tests;

internal sealed record FingerprintReferenceTestData(
    AsymmetricCipherKeyPair RootKeys,
    AsymmetricCipherKeyPair AuthenticationKeys,
    X509Certificate Root,
    X509Certificate Authentication,
    X509Certificate ContentSigner,
    ImmutableArray<byte> Chuid,
    ImmutableArray<byte> Fingerprint
)
{
    internal static async Task<FingerprintReferenceTestData> CreateAsync(bool corrupt)
    {
        var fascn = new FascN(9998, 1, 12345);
        var identity = BiometricIdentity
            .Create(
                FascNCodec.EncodeToBytes(fascn).Value,
                Guid.Parse("909d0ec3-2e77-4a4e-b863-2a3e4352bf48")
            )
            .Value;
        var certificates = FingerprintReferenceCertificates.Create(identity.FascN.ToArray());
        var chuid = SignedChuid(fascn, identity.CardUuid, certificates);
        var fingerprint = await SignReferenceAsync(
            identity,
            certificates.Content,
            certificates.ContentKeys.Private
        );
        if (corrupt)
            fingerprint = fingerprint.SetItem(fingerprint.Length - 1, (byte)(fingerprint[^1] ^ 1));
        return new(
            certificates.RootKeys,
            certificates.AuthenticationKeys,
            certificates.Root,
            certificates.Authentication,
            certificates.Content,
            chuid,
            fingerprint
        );
    }

    internal static ImmutableArray<byte> SignedChuid(
        FascN fascn,
        Guid uuid,
        FingerprintReferenceCertificates certificates
    )
    {
        var chuid = new Chuid
        {
            FascN = fascn,
            Guid = uuid,
            ExpirationDate = VerificationBundleFixture.Now.AddDays(1).UtcDateTime,
        };
        var signature = CeremonyBiometricFixture.Sign(
            ChuidCodec.ToUnsignedBytes(chuid).Value,
            KnownOids.Piv.ChuidSecurityObject.DerObjectIdentifier,
            certificates.Content,
            certificates.ContentKeys.Private,
            []
        );
        return ChuidCodec.ToBytes(chuid, signature).Value;
    }

    private static async Task<ImmutableArray<byte>> SignReferenceAsync(
        BiometricIdentity identity,
        X509Certificate content,
        AsymmetricKeyParameter key
    )
    {
        var unsigned = FingerprintUnsignedReference.Create(identity);
        var verification = new CmsBiometricSignatureVerifier(
            content,
            new CeremonyBiometricFixture.FixtureTrust(),
            [NistObjectIdentifiers.IdSha256.Id],
            [PkcsObjectIdentifiers.Sha256WithRsaEncryption.Id],
            false
        );
        var signed = await BiometricSignatureWorkflow.SignAsync(
            unsigned,
            identity,
            new CeremonyBiometricFixture.FixedTimeSigner(content, key),
            verification
        );
        signed.IsSuccess.Should().BeTrue();
        signed
            .Value.IsSuccess.Should()
            .BeTrue($"{signed.Value.Failure}: {signed.Value.Verification}");
        return CbeffCodec.Write(signed.Value.Record.Value).Value.ToImmutableArray();
    }
}
