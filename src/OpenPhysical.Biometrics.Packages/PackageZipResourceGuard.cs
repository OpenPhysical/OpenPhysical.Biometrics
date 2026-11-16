// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Buffers.Binary;
using CSharpFunctionalExtensions;

internal static class PackageZipResourceGuard
{
    internal static UnitResult<PackageError> Validate(
        ReadOnlySpan<byte> zip,
        PackageProtectionPolicy policy
    )
    {
        if (zip.Length < 22)
            return Invalid();
        var end = zip.Length - 22;
        var trailer = zip[end..];
        if (
            U32(trailer) != 0x06054B50
            || U16(trailer[4..]) != 0
            || U16(trailer[6..]) != 0
            || U16(trailer[20..]) != 0
        )
            return Invalid();
        var count = U16(trailer[10..]);
        if (count > policy.MaximumEntries)
            return Limit();
        var length = U32(trailer[12..]);
        var start = U32(trailer[16..]);
        if (count < 2 || U16(trailer[8..]) != count || (ulong)start + length != (ulong)end)
            return Invalid();
        return CountHeaders(zip, (int)start, end, count, policy.MaximumEntries);
    }

    private static UnitResult<PackageError> CountHeaders(
        ReadOnlySpan<byte> zip,
        int position,
        int end,
        int expected,
        int maximum
    )
    {
        var count = 0;
        while (position < end)
        {
            if (++count > maximum)
                return Limit();
            if (end - position < 46 || U32(zip[position..]) != 0x02014B50)
                return Invalid();
            var header = zip[position..];
            var length = 46 + U16(header[28..]) + U16(header[30..]) + U16(header[32..]);
            if (
                length > end - position
                || U16(header[34..]) != 0
                || U32(header[20..]) == uint.MaxValue
                || U32(header[24..]) == uint.MaxValue
                || U32(header[42..]) == uint.MaxValue
            )
                return Invalid();
            position += length;
        }
        return count == expected ? UnitResult.Success<PackageError>() : Invalid();
    }

    private static ushort U16(ReadOnlySpan<byte> bytes) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes);

    private static uint U32(ReadOnlySpan<byte> bytes) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes);

    private static PackageError Invalid() =>
        new(
            PackageErrorCodes.InvalidArchive,
            "The archive directory structure is invalid or unsupported."
        );

    private static PackageError Limit() =>
        new(
            PackageErrorCodes.LimitExceeded,
            "The archive directory exceeds the configured member limit."
        );
}
