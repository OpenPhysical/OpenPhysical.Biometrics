// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

using System.Buffers.Binary;
using System.Collections.Immutable;

// Appends big-endian fields to one growing buffer. Each call returns the writer so
// serialization composes as an expression. Callers validate ranges beforehand.
internal sealed class BinaryFormatWriter
{
    private readonly List<byte> buffer = [];

    internal int Length => buffer.Count;

    internal BinaryFormatWriter Byte(byte value)
    {
        buffer.Add(value);
        return this;
    }

    internal BinaryFormatWriter UInt16(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return Bytes(bytes);
    }

    internal BinaryFormatWriter UInt32(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return Bytes(bytes);
    }

    // The caller guarantees that value fits in 24 bits.
    internal BinaryFormatWriter UInt24(uint value) =>
        Byte((byte)(value >> 16)).UInt16((ushort)value);

    internal BinaryFormatWriter Bytes(ReadOnlySpan<byte> value)
    {
        buffer.AddRange(value);
        return this;
    }

    internal BinaryFormatWriter Bytes(ImmutableArray<byte> value) => Bytes(value.AsSpan());

    internal byte[] ToArray() => buffer.ToArray();
}
