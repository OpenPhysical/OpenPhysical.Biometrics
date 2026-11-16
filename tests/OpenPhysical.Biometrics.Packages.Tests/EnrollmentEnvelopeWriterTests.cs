// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class EnrollmentEnvelopeWriterTests
{
    [TestCase(0)]
    [TestCase(-1)]
    public void SealRejectsNonpositiveTimestampsBeforeSigning(long seconds)
    {
        var originSigner = new EnrollmentEnvelopeTestKeys();
        var sender = new EnrollmentEnvelopeTestKeys();
        var recipient = new EnrollmentEnvelopeTestKeys();
        var (_, t) = EnrollmentEnvelopeExamples.Create(originSigner, sender, recipient);
        var created = DateTimeOffset.FromUnixTimeSeconds(seconds);
        var changed = new EnrollmentEnvelopeTransfer(
            t.OrganizationId,
            t.TransferId,
            t.SenderId,
            t.RecipientId,
            t.EnrollmentId,
            t.Revision,
            t.Purpose,
            created,
            created.AddHours(1),
            t.AuthorizationReferences,
            t.Origin,
            t.Artifacts
        );
        EnrollmentEnvelopeV2Codec
            .Seal(changed, sender, recipient.SubjectPublicKeyInfo.Span)
            .Should()
            .Fail();
        sender.SigningCount.Should().Be(0);
    }

    [Test]
    public void OriginRejectsCaseInsensitiveDuplicateNamesBeforeSigning()
    {
        var signer = new EnrollmentEnvelopeTestKeys();
        var artifacts = ImmutableArray.Create(
            PackageArtifact.Create("a.bin", [1], "face", "image/test").Value,
            PackageArtifact.Create("A.bin", [2], "face", "image/test").Value
        );
        Create(signer, [], artifacts).Should().Fail();
        signer.SigningCount.Should().Be(0);
    }

    [TestCase("face\n", "image/test")]
    [TestCase("face", "image/test\n")]
    public void OriginRejectsControlCharactersBeforeSigning(string role, string mediaType)
    {
        var signer = new EnrollmentEnvelopeTestKeys();
        var artifact = PackageArtifact.Create("a.bin", [1], role, mediaType).Value;
        Create(signer, [], [artifact]).Should().Fail();
        signer.SigningCount.Should().Be(0);
    }

    [Test]
    public void OriginRejectsOversizedArtifactBeforeSigning()
    {
        var signer = new EnrollmentEnvelopeTestKeys();
        var artifact = PackageArtifact
            .Create("a.bin", new byte[67_108_865], "face", "image/test")
            .Value;
        Create(signer, [], [artifact]).Should().Fail();
        signer.SigningCount.Should().Be(0);
    }

    [Test]
    public void OriginLimitIncludesTheSignatureAndDerWrapper()
    {
        var signer = new EnrollmentEnvelopeTestKeys();
        var artifacts = ImmutableArray.Create(
            PackageArtifact.Create("a.bin", [1], "face", "image/test").Value
        );
        var evidence = new byte[1_048_000];
        var body = EnrollmentOriginArtifactCodec.WriteBody(
            "org",
            "person",
            "enroll",
            1,
            "capture",
            "station",
            evidence,
            artifacts,
            signer.KeyId.ToArray()
        );
        var overhead = body.Length - evidence.Length;
        // The body alone fits, but even the shortest P-256 signature exceeds the remaining space.
        Create(signer, new byte[1_048_575 - overhead], artifacts)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.LimitExceeded);
        var valid = Create(signer, new byte[1_048_490 - overhead], artifacts);
        valid.Should().Succeed();
        EnrollmentOriginArtifactCodec
            .WriteSigned(valid.Value)
            .Length.Should()
            .BeLessThanOrEqualTo(1_048_576);
    }

    private static Result<EnrollmentOrigin, PackageError> Create(
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
}
