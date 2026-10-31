using System.Buffers.Binary;
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Formats;

internal sealed class FormatReadException(FormatDiagnostic diagnostic)
    : Exception(diagnostic.Message)
{
    internal FormatDiagnostic Diagnostic { get; } = diagnostic;
}

// Each nested reader sees only its declared block, so a malformed child cannot
// borrow bytes from the next record. Offsets still refer to the original buffer.
internal ref struct BinaryFormatReader(
    ReadOnlySpan<byte> data,
    int origin,
    List<InspectionField> fields,
    InspectionOptions? options
)
{
    private readonly ReadOnlySpan<byte> data = data;
    internal int Position { get; private set; }
    internal readonly int Remaining => data.Length - Position;
    internal readonly int Offset => origin + Position;

    internal ReadOnlySpan<byte> Read(int length, string name)
    {
        if (length < 0 || length > Remaining)
            Fail(
                "Truncated",
                "The declared field extends beyond its containing block.",
                Offset,
                Math.Max(0, Remaining)
            );
        var value = data.Slice(Position, length);
        fields.Add(
            new InspectionField(
                name,
                Offset,
                length,
                options?.IncludeSensitiveValues == true ? Convert.ToHexString(value) : null
            )
        );
        Position += length;
        return value;
    }

    internal byte Byte(string name) => Read(1, name)[0];

    internal ushort UInt16(string name) => BinaryPrimitives.ReadUInt16BigEndian(Read(2, name));

    internal uint UInt32(string name) => BinaryPrimitives.ReadUInt32BigEndian(Read(4, name));

    internal uint UInt24(string name)
    {
        var bytes = Read(3, name);
        return (uint)(bytes[0] << 16 | bytes[1] << 8 | bytes[2]);
    }

    internal ImmutableArray<byte> Bytes(int length, string name) =>
        ImmutableArray.Create(Read(length, name).ToArray());

    internal readonly void RequireEnd()
    {
        if (Remaining != 0)
            Fail("TrailingData", "The containing block has unconsumed bytes.", Offset, Remaining);
    }

    internal static void Fail(string code, string message, int offset, int length) =>
        throw new FormatReadException(new(code, message, offset, length));
}

internal static class BinaryFormatWriter
{
    internal static void UInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    internal static void UInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    internal static void UInt24(Stream stream, uint value)
    {
        if (value > 0xFFFFFF)
            throw new ArgumentOutOfRangeException(nameof(value));
        stream.WriteByte((byte)(value >> 16));
        UInt16(stream, (ushort)value);
    }
}
