// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;
using Org.BouncyCastle.Asn1;

internal static class EnrollmentEnvelopeV2Open
{
    internal static Result<EnrollmentEnvelopeOpened, PackageError> Open(
        byte[] encoded,
        IEnrollmentKeyResolver resolver,
        IEnrollmentRecipientKey recipient
    )
    {
        if (
            encoded.Length > EnrollmentEnvelopeV2Codec.MaximumEncodedEnvelopeLength
            || encoded.Length < 16
        )
            return EnrollmentEnvelopeV2Encoding.Limit(
                "The envelope length is outside supported limits."
            );
        var root = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(encoded));
        if (root.Count != 3 || !root.GetEncoded(Asn1Encodable.Der).AsSpan().SequenceEqual(encoded))
            return EnrollmentEnvelopeV2Encoding.Invalid(
                "The envelope must be one canonical DER value."
            );
        return AuthenticateEnvelope(
            EnrollmentEnvelopeHeader.Parse(Asn1Sequence.GetInstance(root[0])),
            Asn1Sequence.GetInstance(root[1]),
            root[0],
            root[1],
            Asn1OctetString.GetInstance(root[2]).GetOctets(),
            resolver,
            recipient
        );
    }

    private static Result<EnrollmentEnvelopeOpened, PackageError> AuthenticateEnvelope(
        EnrollmentEnvelopeHeader header,
        Asn1Sequence files,
        Asn1Encodable headerObject,
        Asn1Encodable filesObject,
        byte[] signature,
        IEnrollmentKeyResolver resolver,
        IEnrollmentRecipientKey recipient
    )
    {
        var recipientValid = ValidateRecipient(header, files, recipient);
        if (recipientValid.IsFailure)
            return recipientValid.Error;
        var senderSpki = resolver.ResolveSubjectPublicKeyInfo(header.SenderKeyId);
        if (
            senderSpki.IsFailure
            || !EnrollmentOriginArtifactCodec.KeyIdMatches(senderSpki.Value, header.SenderKeyId)
        )
            return new PackageError(
                PackageErrorCodes.KeyMismatch,
                "The sender signing key is not trusted."
            );
        return VerifySignature(
            header,
            files,
            headerObject,
            filesObject,
            senderSpki.Value,
            signature,
            recipient,
            resolver
        );
    }

    private static Result<bool, PackageError> ValidateRecipient(
        EnrollmentEnvelopeHeader header,
        Asn1Sequence files,
        IEnrollmentRecipientKey recipient
    ) =>
        files.Count == header.FileCount
        && recipient.KeyId.Length == 32
        && EnrollmentOriginArtifactCodec.KeyIdMatches(
            recipient.SubjectPublicKeyInfo.Span,
            recipient.KeyId.Span
        )
        && recipient.KeyId.Span.SequenceEqual(header.RecipientKeyId)
            ? true
            : new PackageError(
                PackageErrorCodes.KeyMismatch,
                "The recipient key does not match the envelope."
            );

    private static Result<EnrollmentEnvelopeOpened, PackageError> VerifySignature(
        EnrollmentEnvelopeHeader header,
        Asn1Sequence files,
        Asn1Encodable headerObject,
        Asn1Encodable filesObject,
        byte[] senderSpki,
        byte[] signature,
        IEnrollmentRecipientKey recipient,
        IEnrollmentKeyResolver resolver
    )
    {
        var signed = new DerSequence(headerObject, filesObject).GetEncoded(Asn1Encodable.Der);
        var digest = EnrollmentEnvelopeV2Encoding.SignedDigest(signed);
        var valid = EnrollmentEnvelopeCrypto.Verify(senderSpki, digest, signature);
        Array.Clear(signed);
        Array.Clear(digest);
        if (!valid)
            return new PackageError(
                PackageErrorCodes.SignatureInvalid,
                "The envelope sender signature is invalid."
            );
        return EnrollmentEnvelopeV2Decrypt.Open(
            header,
            files,
            header.Encode(),
            resolver,
            recipient
        );
    }

    internal static bool Validate(
        EnrollmentEnvelopeTransfer transfer,
        IEnrollmentSigner sender,
        ReadOnlySpan<byte> recipient,
        out PackageError error
    ) => EnrollmentTransferValidation.Validate(transfer, sender, recipient, out error);
}
