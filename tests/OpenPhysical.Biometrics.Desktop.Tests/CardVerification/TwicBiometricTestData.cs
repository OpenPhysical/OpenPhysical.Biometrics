// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Piv;
using OpenPhysical.Middleware.Cbeff.Signatures;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Testing.Piv;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;

namespace OpenPhysical.Middleware.Tests;

internal static class TwicBiometricTestData
{
    internal static async Task<byte[]> CreateAsync(
        BiometricIdentity identity,
        FingerprintReferenceCertificates certificates,
        TwicCardModel generation,
        bool attended
    )
    {
        var unsigned = Unsigned(identity, attended);
        var verifier = Verifier(certificates, generation);
        var signer = new CeremonyBiometricFixture.FixedTimeSigner(
            certificates.Content,
            certificates.ContentKeys.Private,
            omitCardUuid: generation == TwicCardModel.Legacy,
            twicAlias: true
        );
        var record = await SignAsync(unsigned, identity, signer, verifier, generation);
        var roundtrip = CbeffCodec.Parse(CbeffCodec.Write(record).Value);
        roundtrip.IsSuccess.Should().BeTrue("fixture CBEFF roundtrip");
        var checkedBiometric = await BiometricSignatureWorkflow.VerifyAsync(
            roundtrip.Value.Record,
            identity,
            verifier
        );
        checkedBiometric.IsSuccess.Should().BeTrue("fixture biometric verification");
        checkedBiometric
            .Value.IdentityBinding.Should()
            .Be(ValidationStatus.Passed, "fixture biometric identity after roundtrip");
        return CbeffCodec.Write(record).Value;
    }

    private static CbeffRecord Unsigned(BiometricIdentity identity, bool attended)
    {
        var points = Enumerable
            .Range(0, 12)
            .Select(index => new FingerprintMinutia(1, (ushort)(index + 1), 20, 0, 70))
            .ToImmutableArray();
        var record = new FingerprintRecord(
            1,
            2,
            0x8003,
            128,
            128,
            197,
            197,
            [new(2, 0, 0, attended ? (byte)20 : (byte)80, attended ? [] : points, [])]
        );
        var fmr = Incits378Codec.Write(record).Value;
        // TWIC Appendix F ignores the out-of-area point while retaining the original signed bytes.
        if (!attended)
        {
            fmr[30] = 0x40;
            fmr[31] = 128;
        }
        return new(
            FingerprintUnsignedReference.Header(fmr.Length, identity, -2),
            fmr.ToImmutableArray(),
            []
        );
    }

    private static CmsBiometricSignatureVerifier Verifier(
        FingerprintReferenceCertificates certificates,
        TwicCardModel generation
    ) =>
        new(
            certificates.Content,
            new CeremonyBiometricFixture.FixtureTrust(),
            [NistObjectIdentifiers.IdSha256.Id],
            [PkcsObjectIdentifiers.Sha256WithRsaEncryption.Id],
            false,
            allowTwicIdentityAlias: true,
            allowLegacyAbsentUuid: generation == TwicCardModel.Legacy
        );

    private static async Task<CbeffRecord> SignAsync(
        CbeffRecord unsigned,
        BiometricIdentity identity,
        IBiometricSignatureProvider signer,
        CmsBiometricSignatureVerifier verifier,
        TwicCardModel generation
    )
    {
        if (generation == TwicCardModel.Legacy)
        {
            var retained = await SignExistingLegacyReference(unsigned, identity, signer);
            retained.IsSuccess.Should().BeTrue("fixture signature length must converge");
            var check = await BiometricSignatureWorkflow.VerifyAsync(
                retained.Value,
                identity,
                verifier
            );
            check.Value.IdentityBinding.Should().Be(ValidationStatus.Passed);
            return retained.Value;
        }
        var signed = (
            await BiometricSignatureWorkflow.SignAsync(unsigned, identity, signer, verifier)
        ).Value;
        signed.IsSuccess.Should().BeTrue();
        return signed.Record.Value;
    }

    private static async Task<Result<CbeffRecord, string>> SignExistingLegacyReference(
        CbeffRecord unsigned,
        BiometricIdentity identity,
        IBiometricSignatureProvider signer
    )
    {
        var header = unsigned.Header;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var candidate = new CbeffRecord(header, unsigned.BiometricData, []);
            var content = CbeffCodec.GetSignedData(candidate).Value;
            var block = await signer.SignAsync(
                BiometricSignatureRequest.Create(content.AsSpan(), identity).Value
            );
            if (block.Length == header.SignatureLength)
                return new CbeffRecord(header, unsigned.BiometricData, block);
            header = header.WithSignatureLength((ushort)block.Length);
        }
        return Result.Failure<CbeffRecord, string>("fixture.signature-length");
    }
}
