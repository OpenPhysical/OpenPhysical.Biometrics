// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using Org.BouncyCastle.Asn1;

[TestFixture]
internal sealed class EnrollmentEnvelopeV2PointValidationTests
{
    [Test]
    public void InvalidEphemeralPointIsRejectedBeforeRecipientEcdh()
    {
        var originSigner = new EnrollmentEnvelopeTestKeys();
        var sender = new EnrollmentEnvelopeTestKeys();
        var recipient = new EnrollmentEnvelopeTestKeys();
        var (_, transfer) = EnrollmentEnvelopeExamples.Create(originSigner, sender, recipient);
        var encoded = Seal(transfer, sender, recipient);
        var point = new byte[65];
        point[0] = 4;
        var changed = EnrollmentEnvelopeMutationFixtures.ReplaceFirstEncapsulationAndResign(
            encoded,
            sender,
            point
        );
        EnrollmentEnvelopeV2Codec
            .Open(changed, EnrollmentEnvelopeExamples.Resolver(originSigner, sender), recipient)
            .Should()
            .Fail();
        recipient.DerivationCount.Should().Be(0);
    }

    [Test]
    public void HybridPointEncodingIsRejectedBeforeRecipientEcdh()
    {
        var originSigner = new EnrollmentEnvelopeTestKeys();
        var sender = new EnrollmentEnvelopeTestKeys();
        var recipient = new EnrollmentEnvelopeTestKeys();
        var (_, transfer) = EnrollmentEnvelopeExamples.Create(originSigner, sender, recipient);
        var encoded = Seal(transfer, sender, recipient);
        var root = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(encoded));
        var files = Asn1Sequence.GetInstance(root[1]);
        var point = Asn1OctetString.GetInstance(Asn1Sequence.GetInstance(files[0])[1]).GetOctets();
        point[0] = (byte)(0x06 | (point[^1] & 1));
        var changed = EnrollmentEnvelopeMutationFixtures.ReplaceFirstEncapsulationAndResign(
            encoded,
            sender,
            point
        );
        EnrollmentEnvelopeV2Codec
            .Open(changed, EnrollmentEnvelopeExamples.Resolver(originSigner, sender), recipient)
            .Should()
            .Fail();
        recipient.DerivationCount.Should().Be(0);
    }

    private static byte[] Seal(
        EnrollmentEnvelopeTransfer transfer,
        IEnrollmentSigner sender,
        EnrollmentEnvelopeTestKeys recipient
    ) =>
        EnrollmentEnvelopeV2Codec.Seal(transfer, sender, recipient.SubjectPublicKeyInfo.Span).Value;
}
