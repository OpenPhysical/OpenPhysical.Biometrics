// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto.Digests;

internal static class EnrollmentOriginArtifactCodec
{
    private static readonly byte[] IdempotencyDomain =
        "OpenPhysical.EnrollmentOrigin.v2\0"u8.ToArray();
    private static readonly byte[] SignatureDomain =
        "OpenPhysical.EnrollmentOriginSignature.v2\0"u8.ToArray();

    internal static byte[] WriteSigned(EnrollmentOrigin origin) =>
        new DerSequence(
            Asn1Object.FromByteArray(origin.ManifestDer.ToArray()),
            new DerOctetString(origin.Signature.ToArray())
        ).GetEncoded(Asn1Encodable.Der);

    internal static bool ValidateArtifacts(EnrollmentOrigin origin, IReadOnlyList<byte[]> artifacts)
    {
        var body = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(origin.ManifestDer.ToArray()));
        var entries = Asn1Sequence.GetInstance(body[8]);
        if (entries.Count != artifacts.Count)
            return false;
        for (var i = 0; i < entries.Count; i++)
        {
            var item = Asn1Sequence.GetInstance(entries[i]);
            if (!EnrollmentDerIntegers.TryBoundedInt(item[4], 0, 67_108_864, out var length))
                return false;
            var hash = Asn1OctetString.GetInstance(item[5]).GetOctets();
            var actual = EnrollmentOriginArtifactCodec.Digest(artifacts[i]);
            var valid = length == artifacts[i].Length && hash.AsSpan().SequenceEqual(actual);
            Array.Clear(actual);
            if (!valid)
                return false;
        }
        return true;
    }

    internal static EnrollmentOriginCodec.ArtifactInfo ArtifactMetadata(
        EnrollmentOrigin origin,
        int index
    )
    {
        var body = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(origin.ManifestDer.ToArray()));
        var item = Asn1Sequence.GetInstance(Asn1Sequence.GetInstance(body[8])[index]);
        if (!EnrollmentDerIntegers.TryBoundedInt(item[4], 0, 67_108_864, out var length))
            throw new ArgumentException("Origin artifact length is invalid.");
        return new EnrollmentOriginCodec.ArtifactInfo(
            DerUtf8String.GetInstance(item[1]).GetString(),
            DerUtf8String.GetInstance(item[2]).GetString(),
            DerUtf8String.GetInstance(item[3]).GetString(),
            length,
            Asn1OctetString.GetInstance(item[5]).GetOctets()
        );
    }

    internal static string IdempotencyDigest(EnrollmentOrigin origin)
    {
        var data = new byte[IdempotencyDomain.Length + origin.ManifestDer.Length];
        IdempotencyDomain.CopyTo(data, 0);
        origin.ManifestDer.AsSpan().CopyTo(data.AsSpan(IdempotencyDomain.Length));
        var hash = EnrollmentOriginArtifactCodec.Digest(data);
        Array.Clear(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static byte[] WriteBody(
        string org,
        string person,
        string enrollment,
        uint revision,
        string purpose,
        string station,
        byte[] evidence,
        ImmutableArray<PackageArtifact> artifacts,
        byte[] keyId
    ) =>
        new DerSequence(
            DerInteger.ValueOf(2),
            Utf8(org),
            Utf8(person),
            Utf8(enrollment),
            DerInteger.ValueOf(revision),
            Utf8(purpose),
            Utf8(station),
            new DerOctetString(evidence),
            new DerSequence(artifacts.Select(WriteArtifact).ToArray()),
            new DerOctetString(keyId)
        ).GetEncoded(Asn1Encodable.Der);

    private static Asn1Encodable WriteArtifact(PackageArtifact artifact, int index) =>
        new DerSequence(
            DerInteger.ValueOf(index + 1),
            Utf8(artifact.Name),
            Utf8(artifact.Role),
            Utf8(artifact.Format),
            DerInteger.ValueOf(artifact.Bytes.Length),
            new DerOctetString(Convert.FromHexString(artifact.Sha256))
        );

    internal static byte[] Digest(ReadOnlySpan<byte> data)
    {
        var hash = new Sha256Digest();
        hash.BlockUpdate(data);
        var result = new byte[32];
        hash.DoFinal(result);
        return result;
    }

    internal static byte[] SignatureDigest(ReadOnlySpan<byte> statement)
    {
        var input = new byte[SignatureDomain.Length + statement.Length];
        SignatureDomain.CopyTo(input, 0);
        statement.CopyTo(input.AsSpan(SignatureDomain.Length));
        var digest = Digest(input);
        Array.Clear(input);
        return digest;
    }

    internal static bool KeyIdMatches(ReadOnlySpan<byte> spki, ReadOnlySpan<byte> id) =>
        Digest(spki).AsSpan().SequenceEqual(id);

    private static DerUtf8String Utf8(string value) => new(value);

    internal static string Text(Asn1Encodable value) =>
        DerUtf8String.GetInstance(value).GetString();

    internal static bool Integer(Asn1Encodable value, int expected) =>
        EnrollmentDerIntegers.Equals(value, expected);

    internal static bool ValidText(string value, int max) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= max
        && value.All(c => !char.IsControl(c));

    internal static PackageError Invalid(string message) =>
        new(PackageErrorCodes.InvalidTransfer, message);

    internal static PackageError Limit(string message) =>
        new(PackageErrorCodes.LimitExceeded, message);
}
