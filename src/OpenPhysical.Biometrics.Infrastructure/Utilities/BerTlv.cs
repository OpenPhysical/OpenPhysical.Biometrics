namespace OpenPhysical.Biometrics.Infrastructure.Utilities;

using System;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;

/// <summary>
/// Provides functional utilities for parsing and creating BER/DER TLV (Tag-Length-Value) structures.
/// </summary>
/// <remarks>
/// This utility handles the specific case of PIV CBEFF data where tag 0xBC (Biometric Information Template)
/// is marked as constructed but contains primitive data. It provides safe parsing without
/// full ASN.1 validation that would fail on this non-standard encoding.
/// </remarks>
public static class BerTlv
{
    /// <summary>
    /// Immutable result type for TLV parsing operations.
    /// </summary>
    public sealed record TlvParseResult
    {
        /// <summary>Gets the single-byte tag identifying this parsed TLV value.</summary>
        public byte Tag { get; }

        /// <summary>Gets the immutable bytes belonging to the parsed value.</summary>
        public ImmutableArray<byte> Value { get; }

        /// <summary>Gets the total encoded byte count consumed by the tag, length and value.</summary>
        public int BytesConsumed { get; }

        private TlvParseResult(byte tag, ImmutableArray<byte> value, int bytesConsumed)
        {
            Tag = tag;
            Value = value;
            BytesConsumed = bytesConsumed;
        }

        /// <summary>Creates a parsed TLV result after validating the encoded byte count.</summary>
        /// <param name="tag">Single-byte tag identifying the parsed value.</param>
        /// <param name="value">Parsed value bytes.</param>
        /// <param name="bytesConsumed">Number of encoded bytes consumed by this TLV element.</param>
        /// <returns>The parsed tag, value and encoded byte count.</returns>
        public static Result<TlvParseResult> Create(byte tag, byte[] value, int bytesConsumed)
        {
            if (value == null)
                return Result.Failure<TlvParseResult>("Value cannot be null");
            if (bytesConsumed < 2)
                return Result.Failure<TlvParseResult>("Bytes consumed must be at least 2");

            return Result.Success(new TlvParseResult(tag, value.ToImmutableArray(), bytesConsumed));
        }
    }

    /// <summary>
    /// Parses a BER/DER TLV structure from the given data using functional composition.
    /// </summary>
    public static Result<TlvParseResult> ParseTlv(ImmutableArray<byte> data, int offset = 0)
    {
        return ValidateInput(data, offset)
            .Bind(tuple => ParseTag(tuple.data, tuple.offset))
            .Bind(ParseLength)
            .Bind(ExtractValue)
            .Bind(CreateParseResult);
    }

    /// <summary>
    /// Convenience overload for byte arrays.
    /// </summary>
    public static Result<TlvParseResult> ParseTlv(byte[] data, int offset = 0)
    {
        return Maybe<byte[]>
            .From(data)
            .ToResult("Data cannot be null")
            .Map(d => d.ToImmutableArray())
            .Bind(d => ParseTlv(d, offset));
    }

    private static Result<(ImmutableArray<byte> data, int offset)> ValidateInput(
        ImmutableArray<byte> data,
        int offset
    )
    {
        if (data.IsDefault)
            return Result.Failure<(ImmutableArray<byte>, int)>("Data cannot be default");

        if (offset < 0 || offset >= data.Length)
            return Result.Failure<(ImmutableArray<byte>, int)>($"Invalid offset: {offset}");

        if (data.Length - offset < 2)
            return Result.Failure<(ImmutableArray<byte>, int)>(
                "Insufficient data for TLV structure"
            );

        return Result.Success((data, offset));
    }

    private static Result<(byte tag, ImmutableArray<byte> data, int offset)> ParseTag(
        ImmutableArray<byte> data,
        int offset
    )
    {
        return Result.Success((tag: data[offset], data: data, offset: offset + 1));
    }

    private static Result<(
        byte tag,
        int length,
        int lengthBytes,
        ImmutableArray<byte> data,
        int offset
    )> ParseLength((byte tag, ImmutableArray<byte> data, int offset) input)
    {
        return ParseBerLength(input.data, input.offset)
            .Map(result =>
                (
                    tag: input.tag,
                    length: result.length,
                    lengthBytes: result.bytesConsumed,
                    data: input.data,
                    offset: input.offset + result.bytesConsumed
                )
            );
    }

    private static Result<(int length, int bytesConsumed)> ParseBerLength(
        ImmutableArray<byte> data,
        int offset
    )
    {
        if (offset >= data.Length)
            return Result.Failure<(int, int)>("No data for length encoding");

        var firstByte = data[offset];

        return IsShortForm(firstByte)
            ? Result.Success((length: (int)firstByte, bytesConsumed: 1))
            : ParseLongForm(data, offset, firstByte);
    }

    private static bool IsShortForm(byte lengthByte) => (lengthByte & 0x80) == 0;

    private static Result<(int length, int bytesConsumed)> ParseLongForm(
        ImmutableArray<byte> data,
        int offset,
        byte firstByte
    )
    {
        var lengthOfLength = firstByte & 0x7F;

        return ValidateLongForm(lengthOfLength, data.Length - offset - 1)
            .Bind(() => ExtractLongFormLength(data, offset + 1, lengthOfLength))
            .Map(length => (length: length, bytesConsumed: 1 + lengthOfLength));
    }

    private static Result ValidateLongForm(int lengthOfLength, int availableBytes)
    {
        if (lengthOfLength == 0)
            return Result.Failure("Indefinite length encoding not supported");

        if (lengthOfLength > 4)
            return Result.Failure("Length encoding too long (>4 bytes)");

        if (availableBytes < lengthOfLength)
            return Result.Failure("Insufficient data for long form length");

        return Result.Success();
    }

    private static Result<int> ExtractLongFormLength(
        ImmutableArray<byte> data,
        int offset,
        int lengthOfLength
    )
    {
        var length = Enumerable
            .Range(0, lengthOfLength)
            .Select(i => data[offset + i])
            .Aggregate(0, (int acc, byte b) => (acc << 8) | b);

        return Result.Success(length);
    }

    private static Result<(byte tag, byte[] value, int totalBytesConsumed)> ExtractValue(
        (byte tag, int length, int lengthBytes, ImmutableArray<byte> data, int offset) input
    )
    {
        var valueOffset = input.offset;
        var remainingBytes = input.data.Length - valueOffset;

        if (remainingBytes < input.length)
            return Result.Failure<(byte, byte[], int)>(
                $"Insufficient data for value: need {input.length} bytes, have {remainingBytes}"
            );

        var value = input.data.Skip(valueOffset).Take(input.length).ToArray();
        var totalBytesConsumed =
            valueOffset - (input.offset - input.lengthBytes - 1) + input.length;

        return Result.Success(
            (tag: input.tag, value: value, totalBytesConsumed: totalBytesConsumed)
        );
    }

    private static Result<TlvParseResult> CreateParseResult(
        (byte tag, byte[] value, int totalBytesConsumed) input
    )
    {
        return TlvParseResult.Create(input.tag, input.value, input.totalBytesConsumed);
    }

    /// <summary>
    /// Wraps data with a BER/DER TLV structure using functional composition.
    /// </summary>
    public static Result<ImmutableArray<byte>> WrapWithTlv(byte tag, ImmutableArray<byte> value)
    {
        // For wrapper tags, the constructed bit (bit 5) should be set
        if ((tag & 0x20) == 0)
            return Result.Failure<ImmutableArray<byte>>(
                "Invalid wrapper tag: must be constructed (bit 5 set)"
            );

        return EncodeLength(value.Length).Map(lengthBytes => BuildTlv(tag, lengthBytes, value));
    }

    /// <summary>
    /// Convenience overload for byte arrays.
    /// </summary>
    public static Result<byte[]> WrapWithTlv(byte tag, byte[] value)
    {
        return Maybe<byte[]>
            .From(value)
            .ToResult("Value cannot be null")
            .Map(v => v.ToImmutableArray())
            .Bind(v => WrapWithTlv(tag, v))
            .Map(v => v.ToArray());
    }

    private static ImmutableArray<byte> BuildTlv(
        byte tag,
        ImmutableArray<byte> lengthBytes,
        ImmutableArray<byte> value
    )
    {
        return ImmutableArray.Create(tag).AddRange(lengthBytes).AddRange(value);
    }

    private static Result<ImmutableArray<byte>> EncodeLength(int length)
    {
        if (length < 0)
            return Result.Failure<ImmutableArray<byte>>("Length cannot be negative");

        return length < 128
            ? Result.Success(ImmutableArray.Create((byte)length))
            : EncodeLongForm(length);
    }

    private static Result<ImmutableArray<byte>> EncodeLongForm(int length)
    {
        var bytes = GetLengthBytes(length).ToImmutableArray();

        if (bytes.Length > 4)
            return Result.Failure<ImmutableArray<byte>>("Length too large to encode");

        return Result.Success(ImmutableArray.Create((byte)(0x80 | bytes.Length)).AddRange(bytes));
    }

    private static IEnumerable<byte> GetLengthBytes(int length)
    {
        if (length == 0)
            yield return 0;
        else
        {
            var bytes = new List<byte>();
            var temp = length;

            while (temp > 0)
            {
                bytes.Insert(0, (byte)(temp & 0xFF));
                temp >>= 8;
            }

            foreach (var b in bytes)
                yield return b;
        }
    }

    /// <summary>
    /// Checks if data starts with a specific BER/DER tag.
    /// </summary>
    public static bool HasBerTag(ImmutableArray<byte> data, byte expectedTag)
    {
        return !data.IsDefaultOrEmpty && data[0] == expectedTag;
    }

    /// <summary>
    /// Convenience overload for byte arrays.
    /// </summary>
    public static bool HasBerTag(byte[] data, byte expectedTag)
    {
        return data?.Length > 0 && data[0] == expectedTag;
    }

    /// <summary>
    /// Extracts the value from a TLV structure if it has the expected tag, otherwise returns original data.
    /// </summary>
    public static Result<ImmutableArray<byte>> ExtractValueIfTagged(
        ImmutableArray<byte> data,
        byte expectedTag
    )
    {
        if (data.IsDefault)
            return Result.Failure<ImmutableArray<byte>>("Data cannot be default");

        return HasBerTag(data, expectedTag)
            ? ParseTlv(data).Map(result => result.Value)
            : Result.Success(data);
    }

    /// <summary>
    /// Convenience overload for byte arrays.
    /// </summary>
    public static Result<byte[]> ExtractValueIfTagged(byte[] data, byte expectedTag)
    {
        return Maybe<byte[]>
            .From(data)
            .ToResult("Data cannot be null")
            .Map(d => d.ToImmutableArray())
            .Bind(d => ExtractValueIfTagged(d, expectedTag))
            .Map(d => d.ToArray());
    }
}
