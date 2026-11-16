// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;
using Org.BouncyCastle.Asn1;

internal static class EnrollmentOriginReader
{
    internal static Result<EnrollmentOrigin, PackageError> Read(
        byte[] encoded,
        IEnrollmentKeyResolver resolver
    ) =>
        PackageEdge.Bind(
            () => ReadCore(encoded, resolver),
            PackageErrorCodes.InvalidTransfer,
            "The signed origin manifest is malformed."
        );

    private static Result<EnrollmentOrigin, PackageError> ReadCore(
        byte[] encoded,
        IEnrollmentKeyResolver resolver
    )
    {
        if (encoded.Length > 1_048_576)
            return EnrollmentOriginArtifactCodec.Limit("The origin manifest exceeds 1 MiB.");
        if (!EnrollmentDerPreflight.IsSafe(encoded))
            return EnrollmentOriginArtifactCodec.Invalid(
                "The origin manifest DER exceeds supported structure limits."
            );
        var root = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(encoded));
        if (!root.GetEncoded(Asn1Encodable.Der).AsSpan().SequenceEqual(encoded) || root.Count != 2)
            return EnrollmentOriginArtifactCodec.Invalid(
                "The origin manifest is not canonical DER."
            );
        return VerifyBody(
            Asn1Sequence.GetInstance(root[0]),
            Asn1OctetString.GetInstance(root[1]).GetOctets(),
            resolver
        );
    }

    private static Result<EnrollmentOrigin, PackageError> VerifyBody(
        Asn1Sequence body,
        byte[] signature,
        IEnrollmentKeyResolver resolver
    )
    {
        if (body.Count != 10 || !EnrollmentOriginArtifactCodec.Integer(body[0], 2))
            return EnrollmentOriginArtifactCodec.Invalid(
                "The origin manifest version or fields are invalid."
            );
        var keyId = Asn1OctetString.GetInstance(body[9]).GetOctets();
        if (keyId.Length != 32)
            return EnrollmentOriginArtifactCodec.Invalid(
                "The origin signing key identifier is invalid."
            );
        return VerifyWithTrustedKey(body, signature, keyId, resolver);
    }

    private static Result<EnrollmentOrigin, PackageError> VerifyWithTrustedKey(
        Asn1Sequence body,
        byte[] signature,
        byte[] keyId,
        IEnrollmentKeyResolver resolver
    )
    {
        var spki = resolver.ResolveSubjectPublicKeyInfo(keyId);
        if (spki.IsFailure || !EnrollmentOriginArtifactCodec.KeyIdMatches(spki.Value, keyId))
            return new PackageError(
                PackageErrorCodes.KeyMismatch,
                "The origin signing key is not trusted."
            );
        var digest = EnrollmentOriginArtifactCodec.SignatureDigest(
            body.GetEncoded(Asn1Encodable.Der)
        );
        var verified = EnrollmentEnvelopeCrypto.Verify(spki.Value, digest, signature);
        Array.Clear(digest);
        return verified
            ? DecodeBody(body, signature, keyId)
            : new PackageError(
                PackageErrorCodes.SignatureInvalid,
                "The origin signature is invalid."
            );
    }

    private static Result<EnrollmentOrigin, PackageError> DecodeBody(
        Asn1Sequence body,
        byte[] signature,
        byte[] keyId
    )
    {
        var org = EnrollmentOriginArtifactCodec.Text(body[1]);
        var person = EnrollmentOriginArtifactCodec.Text(body[2]);
        var enrollment = EnrollmentOriginArtifactCodec.Text(body[3]);
        if (!EnrollmentDerIntegers.TryUInt(body[4], out var revision))
            return EnrollmentOriginArtifactCodec.Invalid("The origin revision is invalid.");
        var purpose = EnrollmentOriginArtifactCodec.Text(body[5]);
        var station = EnrollmentOriginArtifactCodec.Text(body[6]);
        var evidence = Asn1OctetString.GetInstance(body[7]).GetOctets();
        var artifacts = Asn1Sequence.GetInstance(body[8]);
        if (!ValidFields(org, person, enrollment, purpose, station, evidence, artifacts))
            return EnrollmentOriginArtifactCodec.Invalid("Origin fields exceed supported limits.");
        if (!EnrollmentOriginArtifactValidation.ValidArtifactEntries(artifacts))
            return EnrollmentOriginArtifactCodec.Invalid(
                "Origin artifact metadata, index, or hash is invalid."
            );
        return EnrollmentOrigin.FromParsed(
            org,
            person,
            enrollment,
            revision,
            purpose,
            station,
            evidence,
            body.GetEncoded(Asn1Encodable.Der),
            signature,
            keyId
        );
    }

    private static bool ValidFields(
        string org,
        string person,
        string enrollment,
        string purpose,
        string station,
        byte[] evidence,
        Asn1Sequence artifacts
    ) =>
        EnrollmentOriginArtifactCodec.ValidText(org, 256)
        && EnrollmentOriginArtifactCodec.ValidText(person, 256)
        && EnrollmentOriginArtifactCodec.ValidText(enrollment, 256)
        && EnrollmentOriginArtifactCodec.ValidText(purpose, 64)
        && EnrollmentOriginArtifactCodec.ValidText(station, 256)
        && evidence.Length <= 1_048_576
        && artifacts.Count is >= 1 and <= 63;
}
