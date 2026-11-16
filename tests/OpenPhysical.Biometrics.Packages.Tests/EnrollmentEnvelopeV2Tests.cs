// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using Org.BouncyCastle.Asn1;

[TestFixture]
internal sealed class EnrollmentEnvelopeV2Tests
{
    private static readonly DateTimeOffset Created = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void OpenAuthenticatesOriginAndArtifactsThenReencryptionPreservesOriginDigest()
    {
        var originSigner = new EnrollmentEnvelopeTestKeys();
        var sender = new EnrollmentEnvelopeTestKeys();
        var recipientA = new EnrollmentEnvelopeTestKeys();
        var recipientB = new EnrollmentEnvelopeTestKeys();
        var (origin, transfer) = EnrollmentEnvelopeExamples.Create(
            originSigner,
            sender,
            recipientA
        );
        var first = Seal(transfer, sender, recipientA);
        AssertCanonicalHeader(first);
        var resolver = EnrollmentEnvelopeExamples.Resolver(originSigner, sender);
        var opened = EnrollmentEnvelopeV2Codec.Open(first, resolver, recipientA);
        opened.Should().Succeed();
        opened.Value.Transfer.Origin.ManifestDer.Should().Equal(origin.ManifestDer);
        opened.Value.Transfer.Artifacts[0].Bytes.Should().Equal(transfer.Artifacts[0].Bytes);
        opened.Value.IdempotencyDigest.Should().Be(origin.IdempotencyDigest);

        var second = Seal(Reroute(transfer, origin), sender, recipientB);
        var secondOpen = EnrollmentEnvelopeV2Codec.Open(second, resolver, recipientB);
        secondOpen.Should().Succeed();
        secondOpen.Value.IdempotencyDigest.Should().Be(opened.Value.IdempotencyDigest);
        secondOpen.Value.Transfer.Origin.Signature.Should().Equal(origin.Signature);
    }

    [Test]
    public void InvalidOuterSignatureIsRejectedBeforeRecipientEcdh()
    {
        var originSigner = new EnrollmentEnvelopeTestKeys();
        var sender = new EnrollmentEnvelopeTestKeys();
        var recipient = new EnrollmentEnvelopeTestKeys();
        var (_, transfer) = EnrollmentEnvelopeExamples.Create(originSigner, sender, recipient);
        var bytes = EnrollmentEnvelopeV2Codec
            .Seal(transfer, sender, recipient.SubjectPublicKeyInfo.Span)
            .Value;
        bytes[^1] ^= 1;
        var result = EnrollmentEnvelopeV2Codec.Open(
            bytes,
            EnrollmentEnvelopeExamples.Resolver(originSigner, sender),
            recipient
        );
        result.Should().Fail();
        recipient.DerivationCount.Should().Be(0);
    }

    [Test]
    public void SwappedCiphertextsAndTruncationReleaseNoArtifacts()
    {
        var originSigner = new EnrollmentEnvelopeTestKeys();
        var sender = new EnrollmentEnvelopeTestKeys();
        var recipient = new EnrollmentEnvelopeTestKeys();
        var (origin, transfer) = EnrollmentEnvelopeExamples.Create(
            originSigner,
            sender,
            recipient,
            twoArtifacts: true
        );
        var encoded = EnrollmentEnvelopeV2Codec
            .Seal(transfer, sender, recipient.SubjectPublicKeyInfo.Span)
            .Value;
        var reSignedSwap = EnrollmentEnvelopeMutationFixtures.SwapArtifactCiphertextsAndResign(
            encoded,
            sender
        );
        AssertOpenFails(
            reSignedSwap,
            EnrollmentEnvelopeExamples.Resolver(originSigner, sender),
            recipient
        );
        AssertOpenFails(
            encoded.AsSpan(0, encoded.Length - 1).ToArray(),
            EnrollmentEnvelopeExamples.Resolver(originSigner, sender),
            recipient
        );
        AssertOpenFails(
            [.. encoded, 0],
            EnrollmentEnvelopeExamples.Resolver(originSigner, sender),
            recipient
        );
        origin.IdempotencyDigest.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void InvalidRawEcdhLengthIsClearedBeforeReturningFailure()
    {
        var originSigner = new EnrollmentEnvelopeTestKeys();
        var sender = new EnrollmentEnvelopeTestKeys();
        var recipient = new EnrollmentEnvelopeTestKeys();
        var (_, transfer) = EnrollmentEnvelopeExamples.Create(originSigner, sender, recipient);
        var encoded = EnrollmentEnvelopeV2Codec
            .Seal(transfer, sender, recipient.SubjectPublicKeyInfo.Span)
            .Value;
        var invalidRecipient = new EnrollmentEnvelopeMalformedRecipient(recipient);
        EnrollmentEnvelopeV2Codec
            .Open(
                encoded,
                EnrollmentEnvelopeExamples.Resolver(originSigner, sender),
                invalidRecipient
            )
            .Should()
            .Fail();
        invalidRecipient.RawSecret.Should().OnlyContain(value => value == 0);
    }

    [Test]
    public void UnknownRecipientKeyAndOriginArtifactMismatchAreRejected()
    {
        var originSigner = new EnrollmentEnvelopeTestKeys();
        var sender = new EnrollmentEnvelopeTestKeys();
        var recipient = new EnrollmentEnvelopeTestKeys();
        var wrongRecipient = new EnrollmentEnvelopeTestKeys();
        var (_, transfer) = EnrollmentEnvelopeExamples.Create(originSigner, sender, recipient);
        var encoded = EnrollmentEnvelopeV2Codec
            .Seal(transfer, sender, recipient.SubjectPublicKeyInfo.Span)
            .Value;
        EnrollmentEnvelopeV2Codec
            .Open(
                encoded,
                EnrollmentEnvelopeExamples.Resolver(originSigner, sender),
                wrongRecipient
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.KeyMismatch);
    }

    private static byte[] Seal(
        EnrollmentEnvelopeTransfer transfer,
        IEnrollmentSigner sender,
        EnrollmentEnvelopeTestKeys recipient
    ) =>
        EnrollmentEnvelopeV2Codec.Seal(transfer, sender, recipient.SubjectPublicKeyInfo.Span).Value;

    private static void AssertCanonicalHeader(byte[] envelope)
    {
        var outer = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(envelope));
        EnrollmentEnvelopeHeader
            .Parse(Asn1Sequence.GetInstance(outer[0]))
            .Encode()
            .Should()
            .Equal(outer[0].GetEncoded(Asn1Encodable.Der));
    }

    private static void AssertOpenFails(
        byte[] envelope,
        IEnrollmentKeyResolver resolver,
        IEnrollmentRecipientKey recipient
    ) => EnrollmentEnvelopeV2Codec.Open(envelope, resolver, recipient).Should().Fail();

    private static EnrollmentEnvelopeTransfer Reroute(
        EnrollmentEnvelopeTransfer original,
        EnrollmentOrigin origin
    ) =>
        new(
            original.OrganizationId,
            "transfer-2",
            "station-2",
            "service-2",
            original.EnrollmentId,
            original.Revision,
            EnrollmentTransferPurpose.Delivery,
            Created.AddMinutes(1),
            Created.AddHours(1),
            original.AuthorizationReferences,
            origin,
            original.Artifacts
        );
}
