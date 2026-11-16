// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Asn1;

internal static class EnrollmentEnvelopeV2Decrypt
{
    private const long MaximumAggregate = 134_217_728;
    private const int MaximumManifest = 1_048_576;
    private const long MaximumArtifact = 67_108_864;

    internal static Result<EnrollmentEnvelopeOpened, PackageError> Open(
        EnrollmentEnvelopeHeader header,
        Asn1Sequence files,
        byte[] headerDer,
        IEnrollmentKeyResolver resolver,
        IEnrollmentRecipientKey recipient
    )
    {
        var headerHash = EnrollmentOriginArtifactCodec.Digest(headerDer);
        var plaintexts = new List<byte[]>(files.Count);
        try
        {
            var decrypted = DecryptFiles(headerHash, files, recipient, plaintexts);
            return decrypted.IsFailure
                ? decrypted.Error
                : ValidatePlaintexts(header, plaintexts, resolver);
        }
        finally
        {
            foreach (var plain in plaintexts)
                Array.Clear(plain);
            Array.Clear(headerHash);
            Array.Clear(headerDer);
        }
    }

    private static Result<bool, PackageError> DecryptFiles(
        byte[] headerHash,
        Asn1Sequence files,
        IEnrollmentRecipientKey recipient,
        List<byte[]> plaintexts
    )
    {
        long total = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var plain = DecryptFile(headerHash, files, i, recipient, ref total);
            if (plain.IsFailure)
                return plain.Error;
            plaintexts.Add(plain.Value);
        }
        return true;
    }

    private static Result<byte[], PackageError> DecryptFile(
        byte[] headerHash,
        Asn1Sequence files,
        int index,
        IEnrollmentRecipientKey recipient,
        ref long total
    )
    {
        var item = Asn1Sequence.GetInstance(files[index]);
        if (item.Count != 3 || !EnrollmentDerIntegers.Equals(item[0], index))
            return EnrollmentEnvelopeV2Encoding.Invalid(
                "Encrypted files must use exact ordered indexes."
            );
        var enc = Asn1OctetString.GetInstance(item[1]).GetOctets();
        var ciphertext = Asn1OctetString.GetInstance(item[2]).GetOctets();
        var maximumCiphertext = (index == 0 ? MaximumManifest : MaximumArtifact) + 16;
        if (
            enc.Length != 65
            || enc[0] != 0x04
            || ciphertext.Length < 16
            || ciphertext.Length > maximumCiphertext
        )
            return EnrollmentEnvelopeV2Encoding.Limit(
                "An encrypted file exceeds its supported size."
            );
        total += ciphertext.Length - 16;
        if (total > MaximumAggregate)
            return EnrollmentEnvelopeV2Encoding.Limit("The decrypted enrollment exceeds 128 MiB.");
        return OpenCiphertext(headerHash, files.Count, index, enc, ciphertext, recipient);
    }

    private static byte[] OpenCiphertext(
        byte[] headerHash,
        int count,
        int index,
        byte[] encapsulation,
        byte[] ciphertext,
        IEnrollmentRecipientKey recipient
    )
    {
        var (info, aad) = EnrollmentEnvelopeV2Encoding.Context(headerHash, index, count);
        var plain = EnrollmentEnvelopeCrypto.Open(recipient, encapsulation, info, aad, ciphertext);
        Array.Clear(info);
        Array.Clear(aad);
        return plain;
    }

    private static Result<EnrollmentEnvelopeOpened, PackageError> ValidatePlaintexts(
        EnrollmentEnvelopeHeader header,
        List<byte[]> plaintexts,
        IEnrollmentKeyResolver resolver
    )
    {
        if (plaintexts[0].Length > MaximumManifest)
            return EnrollmentEnvelopeV2Encoding.Limit("The signed origin manifest exceeds 1 MiB.");
        var origin = EnrollmentOriginCodec.Read(plaintexts[0], resolver);
        if (origin.IsFailure)
            return origin.Error;
        if (!MatchesHeaderAndArtifacts(header, origin.Value, plaintexts))
            return EnrollmentEnvelopeV2Encoding.Invalid(
                "The signed origin manifest does not match all decrypted artifacts."
            );
        return CreateOpened(header, origin.Value, plaintexts);
    }

    private static bool MatchesHeaderAndArtifacts(
        EnrollmentEnvelopeHeader header,
        EnrollmentOrigin origin,
        List<byte[]> plaintexts
    ) =>
        origin.OrganizationId == header.OrganizationId
        && origin.EnrollmentId == header.EnrollmentId
        && origin.Revision == header.Revision
        && EnrollmentOriginArtifactCodec.ValidateArtifacts(origin, plaintexts.Skip(1).ToArray());

    private static Result<EnrollmentEnvelopeOpened, PackageError> CreateOpened(
        EnrollmentEnvelopeHeader header,
        EnrollmentOrigin origin,
        List<byte[]> plaintexts
    )
    {
        var artifacts = CreateArtifacts(origin, plaintexts);
        if (artifacts.IsFailure)
            return artifacts.Error;
        var transfer = new EnrollmentEnvelopeTransfer(
            header.OrganizationId,
            header.TransferId,
            header.SenderId,
            header.RecipientId,
            header.EnrollmentId,
            header.Revision,
            header.Purpose,
            DateTimeOffset.FromUnixTimeSeconds(header.CreatedUnixSeconds),
            DateTimeOffset.FromUnixTimeSeconds(header.ExpiresUnixSeconds),
            header.AuthorizationReferences,
            origin,
            artifacts.Value
        );
        return new EnrollmentEnvelopeOpened(
            transfer,
            EnrollmentOriginArtifactCodec.IdempotencyDigest(origin)
        );
    }

    private static Result<ImmutableArray<PackageArtifact>, PackageError> CreateArtifacts(
        EnrollmentOrigin origin,
        List<byte[]> plaintexts
    )
    {
        var artifacts = ImmutableArray.CreateBuilder<PackageArtifact>(plaintexts.Count - 1);
        for (var i = 1; i < plaintexts.Count; i++)
        {
            var metadata = EnrollmentOriginArtifactCodec.ArtifactMetadata(origin, i - 1);
            var artifact = PackageArtifact.Create(
                metadata.Name,
                plaintexts[i],
                metadata.Role,
                metadata.MediaType
            );
            if (artifact.IsFailure)
                return artifact.Error;
            artifacts.Add(artifact.Value);
        }
        return artifacts.MoveToImmutable();
    }
}
