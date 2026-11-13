// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Buffers.Binary;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Security;

internal static class PackageEnvelope
{
    internal const int HeaderLength = 64;
    internal const int TagLength = 16;

    internal static byte[] Create(int plaintextLength)
    {
        var header = new byte[HeaderLength];
        "OPBIOPKG"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(10), HeaderLength);
        header[12] = header[13] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), 65536);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(20), 3);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(24), 4);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(28), (ulong)plaintextLength);
        var random = new SecureRandom();
        random.NextBytes(header.AsSpan(36, 28));
        return header;
    }

    internal static UnitResult<PackageError> Validate(
        ReadOnlySpan<byte> envelope,
        PackageProtectionPolicy policy
    )
    {
        if (
            envelope.Length < HeaderLength + TagLength
            || !envelope[..8].SequenceEqual("OPBIOPKG"u8)
            || BinaryPrimitives.ReadUInt16BigEndian(envelope[8..]) != 1
            || BinaryPrimitives.ReadUInt16BigEndian(envelope[10..]) != HeaderLength
            || envelope[12] != 1
            || envelope[13] != 1
            || envelope[14] != 0
            || envelope[15] != 0
        )
            return Invalid();
        var memory = BinaryPrimitives.ReadUInt32BigEndian(envelope[16..]);
        var iterations = BinaryPrimitives.ReadUInt32BigEndian(envelope[20..]);
        var lanes = BinaryPrimitives.ReadUInt32BigEndian(envelope[24..]);
        var length = BinaryPrimitives.ReadUInt64BigEndian(envelope[28..]);
        if (
            memory is < 65536 or > 262144
            || iterations is < 3 or > 10
            || lanes is < 1 or > 8
            || length > (ulong)policy.MaximumPlaintextBytes
            || length + HeaderLength + TagLength != (ulong)envelope.Length
        )
            return Invalid();
        return UnitResult.Success<PackageError>();
    }

    private static PackageError Invalid() =>
        new(
            PackageErrorCodes.InvalidEnvelope,
            "The package header, supported algorithms, resource bounds, or encoded length is invalid."
        );
}
