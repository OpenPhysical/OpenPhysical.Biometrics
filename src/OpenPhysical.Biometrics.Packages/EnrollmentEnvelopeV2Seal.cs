// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;
using Org.BouncyCastle.Asn1;

internal static class EnrollmentEnvelopeV2Seal
{
    private const long MaximumAggregate = 134_217_728;

    internal static Result<byte[], PackageError> SealCore(
        EnrollmentEnvelopeTransfer transfer,
        IEnrollmentSigner sender,
        byte[] recipientSpki
    )
    {
        if (!EnrollmentEnvelopeV2Open.Validate(transfer, sender, recipientSpki, out var error))
            return error;
        var headerDer = CreateHeader(transfer, sender, recipientSpki, out var headerHash);
        var plaintexts = CreatePlaintexts(transfer);
        try
        {
            var files = EncryptFiles(headerHash, recipientSpki, plaintexts);
            return SignEnvelope(headerDer, files, sender);
        }
        finally
        {
            foreach (var plaintext in plaintexts)
                Array.Clear(plaintext);
            Array.Clear(headerDer);
            Array.Clear(headerHash);
        }
    }

    private static byte[] CreateHeader(
        EnrollmentEnvelopeTransfer transfer,
        IEnrollmentSigner sender,
        byte[] recipientSpki,
        out byte[] headerHash
    )
    {
        var header = new EnrollmentEnvelopeHeader(
            transfer.OrganizationId,
            transfer.TransferId,
            transfer.SenderId,
            transfer.RecipientId,
            transfer.EnrollmentId,
            transfer.Revision,
            sender.KeyId.ToArray(),
            EnrollmentOriginArtifactCodec.Digest(recipientSpki),
            transfer.Purpose,
            transfer.CreatedUtc.ToUnixTimeSeconds(),
            transfer.ExpiresUtc.ToUnixTimeSeconds(),
            transfer.AuthorizationReferences,
            transfer.Artifacts.Length + 1
        );
        var headerDer = header.Encode();
        headerHash = EnrollmentOriginArtifactCodec.Digest(headerDer);
        return headerDer;
    }

    private static List<byte[]> CreatePlaintexts(EnrollmentEnvelopeTransfer transfer)
    {
        var plaintexts = new List<byte[]>
        {
            EnrollmentOriginArtifactCodec.WriteSigned(transfer.Origin),
        };
        plaintexts.AddRange(transfer.Artifacts.Select(artifact => artifact.Bytes.ToArray()));
        return plaintexts;
    }

    private static DerSequence EncryptFiles(
        byte[] headerHash,
        byte[] recipientSpki,
        List<byte[]> plaintexts
    )
    {
        var encrypted = new List<Asn1Encodable>(plaintexts.Count);
        for (var i = 0; i < plaintexts.Count; i++)
            encrypted.Add(EncryptFile(headerHash, recipientSpki, plaintexts, i));
        return new DerSequence(encrypted.ToArray());
    }

    private static Asn1Encodable EncryptFile(
        byte[] headerHash,
        byte[] recipientSpki,
        List<byte[]> plaintexts,
        int index
    )
    {
        var (info, aad) = EnrollmentEnvelopeV2Encoding.Context(headerHash, index, plaintexts.Count);
        var result = EnrollmentEnvelopeCrypto.Seal(recipientSpki, info, aad, plaintexts[index]);
        var value = new DerSequence(
            DerInteger.ValueOf(index),
            new DerOctetString(result.Encapsulation),
            new DerOctetString(result.Ciphertext.ToArray())
        );
        Array.Clear(info);
        Array.Clear(aad);
        Array.Clear(result.Ciphertext);
        return value;
    }

    private static Result<byte[], PackageError> SignEnvelope(
        byte[] headerDer,
        DerSequence files,
        IEnrollmentSigner sender
    )
    {
        var signed = new DerSequence(Asn1Object.FromByteArray(headerDer), files).GetEncoded(
            Asn1Encodable.Der
        );
        var digest = EnrollmentEnvelopeV2Encoding.SignedDigest(signed);
        var signature = sender.SignDigest(digest);
        Array.Clear(signed);
        if (signature.IsFailure)
        {
            Array.Clear(digest);
            return signature.Error;
        }
        return VerifyAndEncode(headerDer, files, digest, signature.Value, sender);
    }

    private static Result<byte[], PackageError> VerifyAndEncode(
        byte[] headerDer,
        DerSequence files,
        byte[] digest,
        byte[] signature,
        IEnrollmentSigner sender
    )
    {
        var validSignature = PackageEdge.Try(
            () =>
                EnrollmentEnvelopeCrypto.Verify(
                    sender.SubjectPublicKeyInfo.Span,
                    digest,
                    signature
                ),
            PackageErrorCodes.CryptoFailed,
            "The envelope signing key is invalid."
        );
        Array.Clear(digest);
        if (validSignature.IsFailure || !validSignature.Value)
            return new PackageError(
                PackageErrorCodes.SignatureInvalid,
                "The envelope signing device returned an invalid P-256 signature."
            );
        return EncodeEnvelope(headerDer, files, signature);
    }

    private static Result<byte[], PackageError> EncodeEnvelope(
        byte[] headerDer,
        DerSequence files,
        byte[] signature
    )
    {
        var output = new DerSequence(
            Asn1Object.FromByteArray(headerDer),
            files,
            new DerOctetString(signature)
        ).GetEncoded(Asn1Encodable.Der);
        return output.Length <= MaximumAggregate + 1_500_000
            ? output
            : new PackageError(PackageErrorCodes.LimitExceeded, "The envelope exceeds 128 MiB.");
    }
}
