// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

using System.Buffers.Binary;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// One reader serves one bounded block of a single parse. Nested readers share the
// located-field list, so every offset refers to the original buffer. Each step
// reports failure as a located FormatError and never throws.
internal sealed class BinaryFormatReader
{
    private readonly ReadOnlyMemory<byte> data;
    private readonly int origin;
    private readonly List<InspectionField> fields;
    private readonly bool includeValues;
    private readonly bool recordFields;
    private int position;

    private BinaryFormatReader(
        ReadOnlyMemory<byte> data,
        int origin,
        List<InspectionField> fields,
        bool includeValues,
        bool recordFields
    )
    {
        this.data = data;
        this.origin = origin;
        this.fields = fields;
        this.includeValues = includeValues;
        this.recordFields = recordFields;
    }

    internal int Position => position;

    internal int Remaining => data.Length - position;

    internal int Offset => origin + position;

    internal static BinaryFormatReader Create(ReadOnlySpan<byte> data, InspectionOptions options) =>
        new(data.ToArray(), 0, [], options.IncludeSensitiveValues, true);

    internal InspectionReport Report() => new(fields.ToImmutableArray(), []);

    internal FormatError Error(string code, string message, int offset, int length)
    {
        var diagnostic = new FormatDiagnostic(code, message, offset, length);
        return new FormatError(
            code,
            message,
            new InspectionReport(fields.ToImmutableArray(), [diagnostic])
        );
    }

    internal Result<T, FormatError> Fail<T>(string code, string message, int offset, int length) =>
        Result.Failure<T, FormatError>(Error(code, message, offset, length));

    internal UnitResult<FormatError> Require(
        bool condition,
        string code,
        string message,
        int offset,
        int length
    ) =>
        condition
            ? UnitResult.Success<FormatError>()
            : UnitResult.Failure(Error(code, message, offset, length));

    internal Result<ReadOnlyMemory<byte>, FormatError> Read(int length, string name)
    {
        if (length < 0 || length > Remaining)
            return Fail<ReadOnlyMemory<byte>>(
                "Truncated",
                "The declared field extends beyond its containing block.",
                Offset,
                Math.Max(0, Remaining)
            );
        var value = data.Slice(position, length);
        if (recordFields)
            fields.Add(new InspectionField(name, Offset, length, Describe(value)));
        position += length;
        return value;
    }

    internal Result<byte, FormatError> Byte(string name) => Read(1, name).Map(v => v.Span[0]);

    internal Result<ushort, FormatError> UInt16(string name) =>
        Read(2, name).Map(v => BinaryPrimitives.ReadUInt16BigEndian(v.Span));

    internal Result<uint, FormatError> UInt32(string name) =>
        Read(4, name).Map(v => BinaryPrimitives.ReadUInt32BigEndian(v.Span));

    internal Result<uint, FormatError> UInt24(string name) =>
        Read(3, name).Map(v => (uint)(v.Span[0] << 16 | v.Span[1] << 8 | v.Span[2]));

    internal Result<ImmutableArray<byte>, FormatError> Bytes(int length, string name) =>
        Read(length, name).Map(v => ImmutableArray.Create(v.ToArray()));

    // Reads consecutive big-endian 16-bit fields, stopping at the first failure.
    internal Result<ImmutableArray<ushort>, FormatError> UInt16s(params string[] names) =>
        ParseSteps.Repeat<ushort>(names.Length, previous => UInt16(names[previous.Count]));

    internal UnitResult<FormatError> Expect(
        string name,
        byte[] expected,
        string code,
        string message,
        int offset
    ) =>
        Read(expected.Length, name)
            .Bind(v =>
                Require(v.Span.SequenceEqual(expected), code, message, offset, expected.Length)
            );

    // Reads a field and returns a reader bounded to exactly that field.
    internal Result<BinaryFormatReader, FormatError> Block(int length, string name)
    {
        var start = Offset;
        return Read(length, name)
            .Map(v => new BinaryFormatReader(v, start, fields, includeValues, recordFields));
    }

    // Returns a recording reader bounded to an already-read block that starts at blockOrigin.
    internal BinaryFormatReader Nested(ReadOnlyMemory<byte> block, int blockOrigin) =>
        new(block, blockOrigin, fields, includeValues, recordFields);

    // Returns a bounded reader that shares error context but adds no inspection fields.
    internal BinaryFormatReader Scratch(ReadOnlyMemory<byte> block, int blockOrigin) =>
        new(block, blockOrigin, fields, false, false);

    internal UnitResult<FormatError> RequireEnd() =>
        Require(
            Remaining == 0,
            "TrailingData",
            "The containing block has unconsumed bytes.",
            Offset,
            Remaining
        );

    private Maybe<string> Describe(ReadOnlyMemory<byte> value) =>
        includeValues ? Maybe.From(Convert.ToHexString(value.Span)) : Maybe<string>.None;
}
