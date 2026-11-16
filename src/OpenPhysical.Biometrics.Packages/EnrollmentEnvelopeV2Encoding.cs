// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

internal static class EnrollmentEnvelopeV2Encoding
{
    private static readonly byte[] SignatureDomain =
        "OpenPhysical.EnrollmentEnvelope.v2\0"u8.ToArray();

    internal static (byte[] Info, byte[] Aad) Context(byte[] headerHash, int index, int count)
    {
        var suffix = new byte[headerHash.Length + 8];
        headerHash.CopyTo(suffix, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
            suffix.AsSpan(headerHash.Length),
            (uint)index
        );
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
            suffix.AsSpan(headerHash.Length + 4),
            (uint)count
        );
        return (
            Join("OpenPhysical.EnrollmentEnvelope.v2\0info"u8.ToArray(), suffix),
            Join("OpenPhysical.EnrollmentEnvelope.v2\0aad"u8.ToArray(), suffix)
        );
    }

    internal static byte[] SignedDigest(byte[] bytes) =>
        EnrollmentOriginArtifactCodec.Digest(Join(SignatureDomain, bytes));

    private static byte[] Join(params byte[][] values)
    {
        var output = new byte[values.Sum(x => x.Length)];
        var offset = 0;
        foreach (var value in values)
        {
            value.CopyTo(output, offset);
            offset += value.Length;
        }
        return output;
    }

    internal static PackageError Invalid(string message) =>
        new(PackageErrorCodes.InvalidEnvelope, message);

    internal static PackageError Limit(string message) =>
        new(PackageErrorCodes.LimitExceeded, message);
}
