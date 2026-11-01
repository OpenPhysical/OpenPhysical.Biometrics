// SPDX-License-Identifier: GPL-2.0-or-later
using System.Buffers.Binary;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging;

internal sealed class TiffDirectory
{
    private readonly ReadOnlyMemory<byte> data;
    private readonly bool little;
    private readonly Dictionary<ushort, (ushort Type, uint Count, int Field)> tags = [];

    private TiffDirectory(ReadOnlyMemory<byte> data, bool little)
    {
        this.data = data;
        this.little = little;
    }

    internal int Length => data.Length;

    internal static Result<TiffDirectory, ImagingError> Parse(ReadOnlyMemory<byte> data)
    {
        if (!BaselineTiff.IsTiff(data.Span))
            return Malformed("The TIFF header is invalid.").Fail<TiffDirectory>();
        var directory = new TiffDirectory(data, data.Span[0] == 'I');
        return directory.UInt32(4).Bind(directory.ReadDirectory);
    }

    internal Result<uint, ImagingError> Scalar(ushort tag) =>
        Values(tag)
            .Ensure(
                values => values.Length == 1,
                Malformed("A TIFF scalar tag has an invalid element count.")
            )
            .Map(values => values[0]);

    internal Result<uint, ImagingError> Scalar(ushort tag, uint fallback) =>
        tags.ContainsKey(tag) ? Scalar(tag) : Result.Success<uint, ImagingError>(fallback);

    internal Result<uint[], ImagingError> Values(ushort tag)
    {
        if (
            !tags.TryGetValue(tag, out var entry)
            || entry.Type is not (3 or 4)
            || entry.Count == 0
            || entry.Count > 65536
            || entry.Count > data.Length / 2
        )
            return Malformed("A required TIFF tag is missing or unsupported.").Fail<uint[]>();
        var size = entry.Type == 3 ? 2 : 4;
        var bytes = (long)entry.Count * size;
        return (
            bytes <= 4 ? Result.Success<uint, ImagingError>((uint)entry.Field) : UInt32(entry.Field)
        )
            .Ensure(
                offset => (ulong)offset + (ulong)bytes <= (ulong)data.Length,
                Malformed("TIFF tag values extend beyond the input.")
            )
            .Map(offset => ReadValues(entry.Count, size, (int)offset));
    }

    private static ImagingError Malformed(string detail) => ImagingErrors.TiffMalformed(detail);

    private Result<TiffDirectory, ImagingError> ReadDirectory(uint ifd)
    {
        if (ifd > int.MaxValue)
            return Malformed("The TIFF directory offset is invalid.").Fail<TiffDirectory>();
        var start = (int)ifd;
        return UInt16(start)
            .Ensure(
                count => (long)start + 2 + 12L * count + 4 <= data.Length,
                Malformed("The TIFF directory is truncated.")
            )
            .Bind(count => ReadTags(start, count));
    }

    private Result<TiffDirectory, ImagingError> ReadTags(int start, uint count)
    {
        var span = data.Span;
        for (var i = 0; i < count; i++)
        {
            var field = start + 2 + i * 12;
            var entry = (Read16(span, field + 2), Read32(span, field + 4), field + 8);
            if (!tags.TryAdd(Read16(span, field), entry))
                return Malformed("The TIFF directory contains duplicate tags.")
                    .Fail<TiffDirectory>();
        }
        return Result.Success<TiffDirectory, ImagingError>(this);
    }

    private uint[] ReadValues(uint count, int size, int offset)
    {
        var span = data.Span;
        var result = new uint[count];
        for (var i = 0; i < result.Length; i++)
            result[i] =
                size == 2 ? Read16(span, offset + i * size) : Read32(span, offset + i * size);
        return result;
    }

    private Result<uint, ImagingError> UInt32(int offset) =>
        offset >= 0 && offset <= data.Length - 4
            ? Result.Success<uint, ImagingError>(Read32(data.Span, offset))
            : Malformed("A TIFF field is truncated.").Fail<uint>();

    private Result<uint, ImagingError> UInt16(int offset) =>
        offset >= 0 && offset <= data.Length - 2
            ? Result.Success<uint, ImagingError>(Read16(data.Span, offset))
            : Malformed("A TIFF field is truncated.").Fail<uint>();

    private ushort Read16(ReadOnlySpan<byte> span, int offset) =>
        little
            ? BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(span.Slice(offset, 2));

    private uint Read32(ReadOnlySpan<byte> span, int offset) =>
        little
            ? BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4))
            : BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset, 4));
}
