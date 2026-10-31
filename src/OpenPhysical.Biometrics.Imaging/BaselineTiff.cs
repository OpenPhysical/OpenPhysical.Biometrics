using System.Buffers.Binary;

namespace OpenPhysical.Biometrics.Imaging;

// The compatibility export uses one uncompressed RGB strip. Keeping this small
// codec local makes the existing TIFF workflow independent of optional encoders.
internal static class BaselineTiff
{
    internal static bool IsTiff(ReadOnlySpan<byte> data) =>
        data.Length >= 4
        && (data[..4].SequenceEqual("II*\0"u8) || data[..4].SequenceEqual("MM\0*"u8));

    internal static void Encode(RgbImage image, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        const ushort entries = 11;
        const int bitsOffset = 8 + 2 + entries * 12 + 4;
        const int pixelOffset = bitsOffset + 6;
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write(new byte[] { (byte)'I', (byte)'I', 42, 0 });
        writer.Write(8u);
        writer.Write(entries);
        Entry(writer, 256, 4, 1, (uint)image.Width);
        Entry(writer, 257, 4, 1, (uint)image.Height);
        Entry(writer, 258, 3, 3, bitsOffset);
        Entry(writer, 259, 3, 1, 1);
        Entry(writer, 262, 3, 1, 2);
        Entry(writer, 273, 4, 1, pixelOffset);
        Entry(writer, 274, 3, 1, 1);
        Entry(writer, 277, 3, 1, 3);
        Entry(writer, 278, 4, 1, (uint)image.Height);
        Entry(writer, 279, 4, 1, checked((uint)(image.Width * image.Height * 3)));
        Entry(writer, 284, 3, 1, 1);
        writer.Write(0u);
        writer.Write((ushort)8);
        writer.Write((ushort)8);
        writer.Write((ushort)8);
        for (var y = 0; y < image.Height; y++)
            foreach (var pixel in image.GetRowSpan(y))
            {
                writer.Write(pixel.R);
                writer.Write(pixel.G);
                writer.Write(pixel.B);
            }
    }

    internal static RgbImage Decode(ReadOnlySpan<byte> data)
    {
        if (!IsTiff(data))
            throw new ArgumentException("The TIFF header is invalid.", nameof(data));
        var little = data[0] == 'I';
        var ifd = UInt32(data, 4, little);
        if (ifd > int.MaxValue)
            throw new ArgumentException("The TIFF directory offset is invalid.", nameof(data));
        var start = (int)ifd;
        var count = UInt16(data, start, little);
        if ((long)start + 2 + 12L * count + 4 > data.Length)
            throw new ArgumentException("The TIFF directory is truncated.", nameof(data));
        var tags = new Dictionary<ushort, (ushort Type, uint Count, int Field)>();
        for (var i = 0; i < count; i++)
        {
            var field = start + 2 + i * 12;
            var tag = UInt16(data, field, little);
            if (
                !tags.TryAdd(
                    tag,
                    (UInt16(data, field + 2, little), UInt32(data, field + 4, little), field + 8)
                )
            )
                throw new ArgumentException(
                    "The TIFF directory contains duplicate tags.",
                    nameof(data)
                );
        }
        var width = checked((int)Scalar(data, tags, 256, little));
        var height = checked((int)Scalar(data, tags, 257, little));
        RgbImage.ValidateDimensions(width, height);
        var samples = checked((int)Scalar(data, tags, 277, little, 1));
        if (
            samples is not (1 or 3 or 4)
            || Scalar(data, tags, 259, little, 1) != 1
            || Scalar(data, tags, 284, little, 1) != 1
            || Scalar(data, tags, 274, little, 1) != 1
        )
            throw new NotSupportedException(
                "TIFF decoding requires uncompressed, top-left, interleaved grayscale or RGB pixels."
            );
        var photometric = Scalar(data, tags, 262, little);
        if ((samples == 1 && photometric is not (0 or 1)) || (samples > 1 && photometric != 2))
            throw new NotSupportedException("The TIFF colour model is unsupported.");
        // RgbaPixel stores straight colour channels. Associated or unspecified extra
        // samples would change their meaning, so require an explicit unassociated alpha tag.
        if (samples == 4 && Scalar(data, tags, 338, little, 0) != 2)
            throw new NotSupportedException(
                "RGBA TIFF decoding requires explicit unassociated alpha."
            );
        var bits = Values(data, tags, 258, little);
        if (bits.Length != samples || bits.Any(value => value != 8))
            throw new NotSupportedException("TIFF decoding requires eight-bit samples.");
        var offsets = Values(data, tags, 273, little);
        var lengths = Values(data, tags, 279, little);
        var rowsPerStrip = Scalar(data, tags, 278, little, (uint)height);
        if (
            rowsPerStrip == 0
            || rowsPerStrip > int.MaxValue
            || offsets.Length == 0
            || offsets.Length != lengths.Length
            || (ulong)offsets.Length != ((ulong)height + rowsPerStrip - 1) / rowsPerStrip
        )
            throw new ArgumentException("The TIFF strip layout is invalid.", nameof(data));
        var result = new RgbImage(width, height);
        try
        {
            var y = 0;
            for (var strip = 0; strip < offsets.Length; strip++)
            {
                var rows = (int)Math.Min(rowsPerStrip, (uint)(height - y));
                var required = checked((long)rows * width * samples);
                if (
                    lengths[strip] != required
                    || (ulong)offsets[strip] + lengths[strip] > (ulong)data.Length
                )
                    throw new ArgumentException(
                        "A TIFF strip has inconsistent bounds.",
                        nameof(data)
                    );
                var position = checked((int)offsets[strip]);
                for (var row = 0; row < rows; row++, y++)
                {
                    var output = result.GetRowSpan(y);
                    for (var x = 0; x < width; x++)
                    {
                        if (samples == 1)
                        {
                            var gray = data[position++];
                            if (photometric == 0)
                                gray = (byte)(255 - gray);
                            output[x] = new(gray);
                        }
                        else
                        {
                            output[x] = new(
                                data[position],
                                data[position + 1],
                                data[position + 2],
                                samples == 4 ? data[position + 3] : (byte)255
                            );
                            position += samples;
                        }
                    }
                }
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static uint Scalar(
        ReadOnlySpan<byte> data,
        Dictionary<ushort, (ushort Type, uint Count, int Field)> tags,
        ushort tag,
        bool little,
        uint? fallback = null
    )
    {
        if (!tags.ContainsKey(tag) && fallback.HasValue)
            return fallback.Value;
        var values = Values(data, tags, tag, little);
        if (values.Length != 1)
            throw new ArgumentException(
                "A TIFF scalar tag has an invalid element count.",
                nameof(data)
            );
        return values[0];
    }

    private static uint[] Values(
        ReadOnlySpan<byte> data,
        Dictionary<ushort, (ushort Type, uint Count, int Field)> tags,
        ushort tag,
        bool little
    )
    {
        if (
            !tags.TryGetValue(tag, out var entry)
            || entry.Type is not (3 or 4)
            || entry.Count == 0
            || entry.Count > 65536
            || entry.Count > data.Length / 2
        )
            throw new ArgumentException(
                "A required TIFF tag is missing or unsupported.",
                nameof(data)
            );
        var size = entry.Type == 3 ? 2 : 4;
        var bytes = (long)entry.Count * size;
        var offset = bytes <= 4 ? (uint)entry.Field : UInt32(data, entry.Field, little);
        if ((ulong)offset + (ulong)bytes > (ulong)data.Length)
            throw new ArgumentException("TIFF tag values extend beyond the input.", nameof(data));
        var result = new uint[checked((int)entry.Count)];
        for (var i = 0; i < result.Length; i++)
            result[i] =
                size == 2
                    ? UInt16(data, (int)offset + i * size, little)
                    : UInt32(data, (int)offset + i * size, little);
        return result;
    }

    private static ushort UInt16(ReadOnlySpan<byte> data, int offset, bool little)
    {
        if (offset < 0 || offset > data.Length - 2)
            throw new ArgumentException("A TIFF field is truncated.", nameof(data));
        return little
            ? BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
    }

    private static uint UInt32(ReadOnlySpan<byte> data, int offset, bool little)
    {
        if (offset < 0 || offset > data.Length - 4)
            throw new ArgumentException("A TIFF field is truncated.", nameof(data));
        return little
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4))
            : BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
    }

    private static void Entry(BinaryWriter writer, ushort tag, ushort type, uint count, uint value)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);
        writer.Write(value);
    }
}
