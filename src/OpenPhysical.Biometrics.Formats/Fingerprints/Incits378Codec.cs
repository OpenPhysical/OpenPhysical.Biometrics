using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Reads and writes complete INCITS 378-2004 records using bounded binary fields.</summary>
public static class Incits378Codec
{
    /// <summary>Parses one complete record. Malformed input produces located diagnostics rather than partial success.</summary>
    /// <param name="data">A raw FMR record without CBEFF or card TLV wrapping.</param>
    /// <param name="options">Explicit opt-in for sensitive values in the inspection report.</param>
    /// <returns>The immutable record and inspection report, or structural diagnostics.</returns>
    public static ParseResult<FingerprintRecord> Parse(
        ReadOnlySpan<byte> data,
        InspectionOptions? options = null
    )
    {
        var fields = new List<InspectionField>();
        try
        {
            var reader = new BinaryFormatReader(data, 0, fields, options);
            if (!reader.Read(4, "FormatIdentifier").SequenceEqual("FMR\0"u8))
                BinaryFormatReader.Fail(
                    "FormatIdentifier",
                    "Expected an INCITS 378 fingerprint record.",
                    0,
                    4
                );
            if (!reader.Read(4, "Version").SequenceEqual(" 20\0"u8))
                BinaryFormatReader.Fail(
                    "UnsupportedVersion",
                    "The fingerprint record version is unsupported.",
                    4,
                    4
                );
            uint length = reader.UInt16("RecordLength");
            if (length == 0)
            {
                length = reader.UInt32("ExtendedRecordLength");
                if (length <= ushort.MaxValue)
                    BinaryFormatReader.Fail(
                        "RecordLength",
                        "The extended length encoding requires a record longer than 65535 bytes.",
                        8,
                        6
                    );
            }
            if (length != data.Length)
                BinaryFormatReader.Fail(
                    "RecordLength",
                    "Declared record length differs from the supplied buffer length.",
                    8,
                    reader.Position - 8
                );
            var owner = reader.UInt16("ProductOwner");
            var product = reader.UInt16("ProductType");
            var equipment = reader.UInt16("CaptureEquipment");
            var width = reader.UInt16("Width");
            var height = reader.UInt16("Height");
            var xResolution = reader.UInt16("XResolution");
            var yResolution = reader.UInt16("YResolution");
            if (width == 0 || height == 0 || xResolution == 0 || yResolution == 0)
                BinaryFormatReader.Fail(
                    "Geometry",
                    "Image dimensions and resolutions must be positive.",
                    reader.Offset - 8,
                    8
                );
            var count = reader.Byte("ViewCount");
            if (count == 0)
                BinaryFormatReader.Fail(
                    "ViewCount",
                    "A fingerprint record must contain at least one view.",
                    reader.Offset - 1,
                    1
                );
            if (reader.Byte("Reserved") != 0)
                BinaryFormatReader.Fail(
                    "ReservedBits",
                    "The reserved header byte must be zero.",
                    reader.Offset - 1,
                    1
                );
            var views = ImmutableArray.CreateBuilder<FingerprintView>(count);
            var seen = new HashSet<(byte, byte)>();
            for (var index = 0; index < count; index++)
            {
                var path = $"Views[{index}]";
                var start = reader.Offset;
                var finger = reader.Byte(path + ".FingerPosition");
                var packed = reader.Byte(path + ".ViewAndImpression");
                var quality = reader.Byte(path + ".Quality");
                var minutiaCount = reader.Byte(path + ".MinutiaCount");
                if (
                    finger > 10
                    || (packed & 15) is not (0 or 1 or 2 or 3 or 8 or 9)
                    || !ValidFingerQuality(quality)
                    || !seen.Add((finger, (byte)(packed >> 4)))
                )
                    BinaryFormatReader.Fail(
                        "FingerView",
                        "The finger view contains an invalid position, quality, or duplicate selection.",
                        start,
                        4
                    );
                var minutiae = ImmutableArray.CreateBuilder<FingerprintMinutia>(minutiaCount);
                for (var m = 0; m < minutiaCount; m++)
                {
                    var prefix = path + $".Minutiae[{m}]";
                    var offset = reader.Offset;
                    var x = reader.UInt16(prefix + ".TypeAndX");
                    var y = reader.UInt16(prefix + ".Y");
                    var angle = reader.Byte(prefix + ".Angle");
                    var pointQuality = reader.Byte(prefix + ".Quality");
                    if (
                        (x >> 14) == 3
                        || (y & 0xC000) != 0
                        || (x & 0x3FFF) >= width
                        || y >= height
                        || angle > 179
                        || pointQuality > 100
                    )
                        BinaryFormatReader.Fail(
                            "Minutia",
                            "A minutia has an invalid type, coordinate, angle, quality, or reserved bit.",
                            offset,
                            6
                        );
                    minutiae.Add(
                        new((byte)(x >> 14), (ushort)(x & 0x3FFF), y, angle, pointQuality)
                    );
                }
                var extendedLength = reader.UInt16(path + ".ExtendedLength");
                var extendedOrigin = reader.Offset;
                var extended = reader.Read(extendedLength, path + ".ExtendedData");
                var blocksReader = new BinaryFormatReader(
                    extended,
                    extendedOrigin,
                    fields,
                    options
                );
                var blocks = ImmutableArray.CreateBuilder<FingerprintExtendedBlock>();
                while (blocksReader.Remaining > 0)
                {
                    var blockPath = path + $".ExtendedBlocks[{blocks.Count}]";
                    var blockOffset = blocksReader.Offset;
                    var type = blocksReader.UInt16(blockPath + ".Type");
                    var blockLength = blocksReader.UInt16(blockPath + ".Length");
                    if (blockLength < 4)
                        BinaryFormatReader.Fail(
                            "ExtendedBlockLength",
                            "An extended block length must include its four-byte header.",
                            blockOffset + 2,
                            2
                        );
                    var blockData = blocksReader.Bytes(blockLength - 4, blockPath + ".Data");
                    ValidateExtendedBlock(type, blockData.AsSpan(), minutiaCount, blockOffset + 4);
                    blocks.Add(new(type, blockData));
                }
                views.Add(
                    new(
                        finger,
                        (byte)(packed >> 4),
                        (byte)(packed & 15),
                        quality,
                        minutiae.MoveToImmutable(),
                        blocks.ToImmutable()
                    )
                );
            }
            reader.RequireEnd();
            return new(
                new(
                    owner,
                    product,
                    equipment,
                    width,
                    height,
                    xResolution,
                    yResolution,
                    views.MoveToImmutable()
                ),
                fields,
                []
            );
        }
        catch (FormatReadException error)
        {
            return new(null, fields, [error.Diagnostic]);
        }
    }

    /// <summary>Serializes a record, deriving all lengths and counts from its immutable collections.</summary>
    /// <param name="record">The record to write.</param>
    /// <returns>Complete FMR bytes, owned by the caller.</returns>
    /// <exception cref="ArgumentException">A field cannot be represented by INCITS 378-2004.</exception>
    public static byte[] Write(FingerprintRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Views.IsDefault || record.Views.Length > 255)
            throw new ArgumentException(
                "A record must contain an initialized collection of at most 255 views.",
                nameof(record)
            );
        using var body = new MemoryStream();
        BinaryFormatWriter.UInt16(body, record.ProductOwner);
        BinaryFormatWriter.UInt16(body, record.ProductType);
        BinaryFormatWriter.UInt16(body, record.CaptureEquipment);
        BinaryFormatWriter.UInt16(body, record.Width);
        BinaryFormatWriter.UInt16(body, record.Height);
        BinaryFormatWriter.UInt16(body, record.XResolution);
        BinaryFormatWriter.UInt16(body, record.YResolution);
        body.WriteByte((byte)record.Views.Length);
        body.WriteByte(0);
        foreach (var view in record.Views)
        {
            if (
                view is null
                || view.ViewNumber > 15
                || view.ImpressionType > 15
                || view.Minutiae.IsDefault
                || view.Minutiae.Length > 255
                || view.ExtendedBlocks.IsDefault
            )
                throw new ArgumentException(
                    "A finger view has invalid or uninitialized fields.",
                    nameof(record)
                );
            body.WriteByte(view.FingerPosition);
            body.WriteByte((byte)(view.ViewNumber << 4 | view.ImpressionType));
            body.WriteByte(view.Quality);
            body.WriteByte((byte)view.Minutiae.Length);
            foreach (var point in view.Minutiae)
            {
                if (point is null || point.X > 0x3FFF || point.Y > 0x3FFF || point.Type > 2)
                    throw new ArgumentException("A minutia cannot be represented.", nameof(record));
                BinaryFormatWriter.UInt16(body, (ushort)(point.Type << 14 | point.X));
                BinaryFormatWriter.UInt16(body, point.Y);
                body.WriteByte(point.Angle);
                body.WriteByte(point.Quality);
            }
            using var extended = new MemoryStream();
            foreach (var block in view.ExtendedBlocks)
            {
                if (
                    block is null
                    || block.Data.IsDefault
                    || block.Data.Length > ushort.MaxValue - 4
                )
                    throw new ArgumentException(
                        "An extended block cannot be represented.",
                        nameof(record)
                    );
                BinaryFormatWriter.UInt16(extended, block.Type);
                BinaryFormatWriter.UInt16(extended, (ushort)(block.Data.Length + 4));
                extended.Write(block.Data.AsSpan());
            }
            if (extended.Length > ushort.MaxValue)
                throw new ArgumentException(
                    "The extended area exceeds its length field.",
                    nameof(record)
                );
            BinaryFormatWriter.UInt16(body, (ushort)extended.Length);
            extended.Position = 0;
            extended.CopyTo(body);
        }
        using var result = new MemoryStream();
        result.Write("FMR\0 20\0"u8);
        var shortLength = checked(body.Length + 10);
        if (shortLength <= ushort.MaxValue)
            BinaryFormatWriter.UInt16(result, (ushort)shortLength);
        else
        {
            BinaryFormatWriter.UInt16(result, 0);
            BinaryFormatWriter.UInt32(result, checked((uint)(shortLength + 4)));
        }
        body.Position = 0;
        body.CopyTo(result);
        var bytes = result.ToArray();
        var validation = Parse(bytes);
        if (!validation.IsSuccess)
            throw new ArgumentException(validation.Diagnostics[0].Message, nameof(record));
        return bytes;
    }

    private static bool ValidFingerQuality(byte quality) => quality <= 100 || quality is 254 or 255;

    private static void ValidateExtendedBlock(
        ushort type,
        ReadOnlySpan<byte> data,
        int minutiaeCount,
        int origin
    )
    {
        if (type == 0 || (type > 2 && (type < 0x100 || (type & 0xFF) == 0)))
            BinaryFormatReader.Fail(
                "ExtendedBlockType",
                "The extended block uses a reserved type identifier.",
                origin - 4,
                2
            );
        if (type == 1)
        {
            if (data.Length < 1 || data[0] > 2 || (data.Length - 1) % 3 != 0)
                BinaryFormatReader.Fail(
                    "RidgeCountBlock",
                    "The ridge-count block has an invalid extraction method or entry length.",
                    origin,
                    data.Length
                );
            for (var i = 1; i < data.Length; i += 3)
            {
                if (data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 0)
                    continue;
                if (
                    data[0] is 1 or 2
                    && data[i] > 0
                    && data[i] <= minutiaeCount
                    && data[i + 1] == 0
                    && data[i + 2] == 0
                )
                    continue;
                if (
                    data[i] == 0
                    || data[i + 1] == 0
                    || data[i] > minutiaeCount
                    || data[i + 1] > minutiaeCount
                )
                    BinaryFormatReader.Fail(
                        "RidgeCountIndex",
                        "A ridge-count entry refers to a minutia outside this view.",
                        origin + i,
                        3
                    );
            }
        }
        else if (type == 2)
        {
            var fields = new List<InspectionField>();
            var reader = new BinaryFormatReader(data, origin, fields, null);
            for (var group = 0; group < 2; group++)
            {
                var info = reader.Byte(group == 0 ? "CoreInfo" : "DeltaInfo");
                if ((info & 0x30) != 0 || (info >> 6) > 1)
                    BinaryFormatReader.Fail(
                        "CoreDeltaInfo",
                        "Core or delta information contains reserved bits.",
                        reader.Offset - 1,
                        1
                    );
                for (var i = 0; i < (info & 15); i++)
                {
                    var pointOffset = reader.Offset;
                    var x = reader.UInt16("X");
                    var y = reader.UInt16("Y");
                    if ((x & 0xC000) != 0 || (y & 0xC000) != 0)
                        BinaryFormatReader.Fail(
                            "CoreDeltaCoordinate",
                            "Core or delta coordinate contains reserved bits.",
                            pointOffset,
                            4
                        );
                    // Core and delta information use opposite angle-presence flags.
                    if ((group == 0 && (info >> 6) == 1) || (group == 1 && (info >> 6) == 0))
                    {
                        var angles = reader.Read(group == 0 ? 1 : 3, "Angles");
                        foreach (var angle in angles)
                            if (angle > 179)
                                BinaryFormatReader.Fail(
                                    "CoreDeltaAngle",
                                    "A core or delta angle exceeds 179 two-degree units.",
                                    reader.Offset - angles.Length,
                                    angles.Length
                                );
                    }
                }
            }
            reader.RequireEnd();
        }
    }
}
