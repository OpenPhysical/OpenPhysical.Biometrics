namespace OpenPhysical.Biometrics.Infrastructure.Utilities;

using System.Buffers.Binary;
using System.Text;

/// <summary>
/// A binary reader that reads multi-byte values in big-endian (network) byte order.
/// </summary>
public sealed class BigEndianBinaryReader : IDisposable
{
    private readonly BinaryReader _reader;
    private readonly Encoding _encoding;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BigEndianBinaryReader"/> class.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    public BigEndianBinaryReader(Stream stream)
    {
        _encoding = Encoding.UTF8;
        _reader = new BinaryReader(stream, _encoding, leaveOpen: false);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BigEndianBinaryReader"/> class.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="encoding">The encoding to use for string operations.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when disposing.</param>
    public BigEndianBinaryReader(Stream stream, Encoding encoding, bool leaveOpen = false)
    {
        _encoding = encoding;
        _reader = new BinaryReader(stream, encoding, leaveOpen);
    }

    /// <summary>
    /// Gets the underlying stream.
    /// </summary>
    public Stream BaseStream => _reader.BaseStream;

    /// <summary>
    /// Reads a byte from the stream.
    /// </summary>
    public byte ReadByte() => _reader.ReadByte();

    /// <summary>
    /// Reads a signed byte from the stream.
    /// </summary>
    public sbyte ReadSByte() => _reader.ReadSByte();

    /// <summary>
    /// Reads a 16-bit unsigned integer in big-endian byte order.
    /// </summary>
    public ushort ReadUInt16()
    {
        Span<byte> buffer = stackalloc byte[2];
        _reader.Read(buffer);
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    /// <summary>
    /// Reads a 16-bit signed integer in big-endian byte order.
    /// </summary>
    public short ReadInt16()
    {
        Span<byte> buffer = stackalloc byte[2];
        _reader.Read(buffer);
        return BinaryPrimitives.ReadInt16BigEndian(buffer);
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer in big-endian byte order.
    /// </summary>
    public uint ReadUInt32()
    {
        Span<byte> buffer = stackalloc byte[4];
        _reader.Read(buffer);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    /// <summary>
    /// Reads a 32-bit signed integer in big-endian byte order.
    /// </summary>
    public int ReadInt32()
    {
        Span<byte> buffer = stackalloc byte[4];
        _reader.Read(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer in big-endian byte order.
    /// </summary>
    public ulong ReadUInt64()
    {
        Span<byte> buffer = stackalloc byte[8];
        _reader.Read(buffer);
        return BinaryPrimitives.ReadUInt64BigEndian(buffer);
    }

    /// <summary>
    /// Reads a 64-bit signed integer in big-endian byte order.
    /// </summary>
    public long ReadInt64()
    {
        Span<byte> buffer = stackalloc byte[8];
        _reader.Read(buffer);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    /// <summary>
    /// Reads a specified number of bytes from the stream.
    /// </summary>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>A byte array containing the read bytes.</returns>
    public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

    /// <summary>
    /// Reads bytes into the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer to read into.</param>
    /// <returns>The number of bytes read.</returns>
    public int Read(byte[] buffer) => _reader.Read(buffer);

    /// <summary>
    /// Reads bytes into the specified span.
    /// </summary>
    /// <param name="buffer">The span to read into.</param>
    /// <returns>The number of bytes read.</returns>
    public int Read(Span<byte> buffer) => _reader.Read(buffer);

    /// <summary>
    /// Reads a string of the specified length using the current encoding.
    /// </summary>
    /// <param name="length">The number of bytes to read.</param>
    /// <returns>The decoded string.</returns>
    public string ReadString(int length)
    {
        var bytes = ReadBytes(length);
        return _encoding.GetString(bytes);
    }

    /// <summary>
    /// Skips the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to skip.</param>
    public void Skip(int count)
    {
        if (BaseStream.CanSeek)
        {
            BaseStream.Seek(count, SeekOrigin.Current);
        }
        else
        {
            // For non-seekable streams, read and discard
            var buffer = new byte[Math.Min(count, 4096)];
            var remaining = count;
            while (remaining > 0)
            {
                var toRead = Math.Min(remaining, buffer.Length);
                var read = _reader.Read(buffer, 0, toRead);
                if (read == 0)
                    break;
                remaining -= read;
            }
        }
    }

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
    /// Disposes the reader and underlying stream.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _reader.Dispose();
            _disposed = true;
        }
    }
}
