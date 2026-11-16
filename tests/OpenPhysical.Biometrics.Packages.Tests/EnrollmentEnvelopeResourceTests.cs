// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using Org.BouncyCastle.Asn1;

[TestFixture]
internal sealed class EnrollmentEnvelopeResourceTests
{
    [Test]
    public void OriginManifestEvidenceHasAMaximumSize()
    {
        var signer = new EnrollmentEnvelopeTestKeys();
        var artifact = PackageArtifact.Create("face.bin", [1], "portrait", "image/test").Value;
        var artifacts = ImmutableArray.Create(artifact);
        CreateOrigin(signer, new byte[1_048_000], artifacts).Should().Succeed();
        CreateOrigin(signer, new byte[1_048_500], artifacts)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.LimitExceeded);
    }

    [Test]
    public void OriginAllowsAtMost63Artifacts()
    {
        var signer = new EnrollmentEnvelopeTestKeys();
        var artifact = PackageArtifact.Create("face.bin", [1], "portrait", "image/test").Value;
        var sixtyThree = ImmutableArray.CreateBuilder<PackageArtifact>(63);
        for (var i = 0; i < 63; i++)
            sixtyThree.Add(
                PackageArtifact
                    .Create($"a{i:00}.bin", [(byte)i], "source", "application/test")
                    .Value
            );
        CreateOrigin(signer, "{}"u8.ToArray(), sixtyThree.ToImmutable()).Should().Succeed();
        var sixtyFour = sixtyThree.ToImmutable().Add(artifact);
        CreateOrigin(signer, "{}"u8.ToArray(), sixtyFour)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.InvalidTransfer);
    }

    private static Result<EnrollmentOrigin, PackageError> CreateOrigin(
        EnrollmentEnvelopeTestKeys signer,
        byte[] evidence,
        ImmutableArray<PackageArtifact> artifacts
    ) =>
        EnrollmentOrigin.Create(
            "org",
            "person",
            "enroll",
            1,
            "capture",
            "station",
            evidence,
            artifacts,
            signer
        );

    [Test]
    public void HeaderRejectsSixtyFiveFilesBeforeRecipientEcdh()
    {
        var recipient = new EnrollmentEnvelopeTestKeys();
        var header = new EnrollmentEnvelopeHeader(
            "org",
            "transfer",
            "station",
            "service",
            "enroll",
            1,
            recipient.KeyId.ToArray(),
            recipient.KeyId.ToArray(),
            EnrollmentTransferPurpose.Upload,
            1_795_200_000,
            1_795_203_600,
            [],
            65
        );
        var invalid = new DerSequence(
            Asn1Object.FromByteArray(header.Encode()),
            new DerSequence(Array.Empty<Asn1Encodable>()),
            new DerOctetString([])
        ).GetEncoded(Asn1Encodable.Der);
        EnrollmentEnvelopeV2Codec
            .Open(invalid, new EnrollmentEnvelopeTestResolver(), recipient)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.InvalidEnvelope);
        recipient.DerivationCount.Should().Be(0);
    }

    [Test]
    public void HeaderRejectsNegativeAndOversizedIntegerAliases()
    {
        var key = new EnrollmentEnvelopeTestKeys();
        var header = new EnrollmentEnvelopeHeader(
            "org",
            "transfer",
            "station",
            "service",
            "enroll",
            1,
            key.KeyId.ToArray(),
            key.KeyId.ToArray(),
            EnrollmentTransferPurpose.Upload,
            1_795_200_000,
            1_795_203_600,
            [],
            1
        );
        RejectInteger(header, 7, DerInteger.ValueOf(-1));
        RejectInteger(header, 7, DerInteger.ValueOf(4_294_967_297L));
        RejectInteger(header, 10, DerInteger.ValueOf(4_294_967_298L));
        var tooLargeTime = Org
            .BouncyCastle.Math.BigInteger.ValueOf(long.MaxValue)
            .Add(Org.BouncyCastle.Math.BigInteger.One);
        RejectInteger(header, 11, new DerInteger(tooLargeTime));
        RejectInteger(header, 12, DerInteger.ValueOf(253_402_300_800));
    }

    [Test]
    public void EnvelopeRejectsDeepOrBroadDerBeforeAsn1Parsing()
    {
        var recipient = new EnrollmentEnvelopeTestKeys();
        var resolver = new EnrollmentEnvelopeTestResolver();
        EnrollmentEnvelopeV2Codec.Open(NestSequences(20), resolver, recipient).Should().Fail();
        var children = Enumerable
            .Repeat(new byte[] { 0x02, 0x01, 0x00 }, 2_049)
            .SelectMany(value => value)
            .ToArray();
        EnrollmentEnvelopeV2Codec.Open(WrapSequence(children), resolver, recipient).Should().Fail();
        recipient.DerivationCount.Should().Be(0);
    }

    [TestCase("300105")]
    [TestCase("30020480")]
    public void DerPreflightPropagatesRejectedFinalChild(string encoded)
    {
        EnrollmentDerPreflight.IsSafe(Convert.FromHexString(encoded)).Should().BeFalse();
    }

    private static byte[] NestSequences(int count)
    {
        var value = new byte[] { 0x02, 0x01, 0x00 };
        for (var i = 0; i < count; i++)
            value = WrapSequence(value);
        return value;
    }

    private static byte[] WrapSequence(byte[] content)
    {
        var prefix =
            content.Length < 128
                ? new byte[] { 0x30, (byte)content.Length }
                : [0x30, 0x82, (byte)(content.Length >> 8), (byte)content.Length];
        return [.. prefix, .. content];
    }

    private static void RejectInteger(
        EnrollmentEnvelopeHeader header,
        int field,
        DerInteger replacement
    )
    {
        var values = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(header.Encode())).ToArray();
        values[field] = replacement;
        var encoded = Asn1Sequence.GetInstance(new DerSequence(values));
        var parsed = () => EnrollmentEnvelopeHeader.Parse(encoded);
        parsed.Should().Throw<ArgumentException>();
    }
}
