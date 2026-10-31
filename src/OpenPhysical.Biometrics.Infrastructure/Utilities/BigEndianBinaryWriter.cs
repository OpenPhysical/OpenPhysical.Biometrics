namespace OpenPhysical.Biometrics.Infrastructure.Utilities;

using System.Buffers.Binary;
using System.Text;

/// <summary>
/// A binary writer that writes multi-byte values in big-endian (network) byte order.
/// </summary>
public sealed class BigEndianBinaryWriter : IDisposable
{
    private readonly BinaryWriter _writer;
    private readonly Encoding _encoding;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BigEndianBinaryWriter"/> class.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public BigEndianBinaryWriter(Stream stream)
    {
        _encoding = Encoding.UTF8;
        _writer = new BinaryWriter(stream, _encoding, leaveOpen: false);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BigEndianBinaryWriter"/> class.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="encoding">The encoding to use for string operations.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when disposing.</param>
    public BigEndianBinaryWriter(Stream stream, Encoding encoding, bool leaveOpen = false)
    {
        _encoding = encoding;
        _writer = new BinaryWriter(stream, encoding, leaveOpen);
    }

    /// <summary>
    /// Gets the underlying stream.
    /// </summary>
    public Stream BaseStream => _writer.BaseStream;

    /// <summary>
    /// Writes a byte to the stream.
    /// </summary>
    public void Write(byte value) => _writer.Write(value);

    /// <summary>
    /// Writes a signed byte to the stream.
    /// </summary>
    public void Write(sbyte value) => _writer.Write(value);

    /// <summary>
    /// Writes a 16-bit unsigned integer in big-endian byte order.
    /// </summary>
    public void Write(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        _writer.Write(buffer);
    }

    /// <summary>
    /// Writes a 16-bit signed integer in big-endian byte order.
    /// </summary>
    public void Write(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        _writer.Write(buffer);
    }

    /// <summary>
    /// Writes a 32-bit unsigned integer in big-endian byte order.
    /// </summary>
    public void Write(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        _writer.Write(buffer);
    }

    /// <summary>
    /// Writes a 32-bit signed integer in big-endian byte order.
    /// </summary>
    public void Write(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        _writer.Write(buffer);
    }

    /// <summary>
    /// Writes a 64-bit unsigned integer in big-endian byte order.
    /// </summary>
    public void Write(ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        _writer.Write(buffer);
    }

    /// <summary>
    /// Writes a 64-bit signed integer in big-endian byte order.
    /// </summary>
    public void Write(long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        _writer.Write(buffer);
    }

    /// <summary>
    /// Writes a byte array to the stream.
    /// </summary>
    public void Write(byte[] bytes) => _writer.Write(bytes);

    /// <summary>
    /// Writes a portion of a byte array to the stream.
    /// </summary>
    public void Write(byte[] bytes, int index, int count) => _writer.Write(bytes, index, count);

    /// <summary>
    /// Writes bytes from a span to the stream.
    /// </summary>
    public void Write(ReadOnlySpan<byte> bytes) => _writer.Write(bytes);

    /// <summary>
    /// Writes a string using the specified encoding and length.
    /// </summary>
    /// <param name="value">The string to write.</param>
    /// <param name="length">The exact number of bytes to write (pads with zeros if needed).</param>
    public void WriteFixedString(string value, int length)
    {
        var bytes = _encoding.GetBytes(value);
        if (bytes.Length > length)
        {
            _writer.Write(bytes, 0, length);
        }
        else
        {
            _writer.Write(bytes);
            // Pad with zeros if needed
            for (var i = bytes.Length; i < length; i++)
            {
                _writer.Write((byte)0);
            }
        }
    }

    /// <summary>
    /// Writes a string as UTF-8 bytes without length prefix.
    /// </summary>
    public void WriteString(string value)
    {
        var bytes = _encoding.GetBytes(value);
        _writer.Write(bytes);
    }

    /// <summary>
    /// Writes padding bytes (zeros) to align to the specified boundary.
    /// </summary>
    /// <param name="boundary">The alignment boundary (e.g., 4 for 32-bit alignment).</param>
    public void WritePadding(int boundary)
    {
        var position = BaseStream.Position;
        var padding = (boundary - (position % boundary)) % boundary;
        for (var i = 0; i < padding; i++)
        {
            Write((byte)0);
        }
    }

    /// <summary>
    /// Flushes the underlying stream.
    /// </summary>
    public void Flush() => _writer.Flush();

    /// <summary>
    /// Gets the current position in the stream (if seekable).
    /// </summary>
    public long Position
    {
        get => BaseStream.Position;
        set => BaseStream.Position = value;
    }

    /// <summary>
    /// Gets the length of the stream (if available).
    /// </summary>
    public long Length => BaseStream.Length;

    /// <summary>
    /// Disposes the writer and underlying stream.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _writer.Dispose();
            _disposed = true;
        }
    }
}
