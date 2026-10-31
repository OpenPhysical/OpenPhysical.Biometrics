using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Formats.Faces;

/// <summary>Reads and writes complete INCITS 385-2004 facial records, including multiple image blocks.</summary>
public static class Incits385Codec
{
    /// <summary>Parses one complete facial interchange record with bounded image blocks.</summary>
    /// <param name="data">Raw FAC record bytes.</param>
    /// <param name="options">Explicit opt-in for sensitive values in inspection output.</param>
    /// <returns>An immutable record or located structural diagnostics.</returns>
    public static ParseResult<FaceRecord> Parse(
        ReadOnlySpan<byte> data,
        InspectionOptions? options = null
    )
    {
        var fields = new List<InspectionField>();
        try
        {
            var reader = new BinaryFormatReader(data, 0, fields, options);
            if (!reader.Read(4, "FormatIdentifier").SequenceEqual("FAC\0"u8))
                BinaryFormatReader.Fail(
                    "FormatIdentifier",
                    "Expected an INCITS 385 facial record.",
                    0,
                    4
                );
            if (!reader.Read(4, "Version").SequenceEqual("010\0"u8))
                BinaryFormatReader.Fail(
                    "UnsupportedVersion",
                    "The facial record version is unsupported.",
                    4,
                    4
                );
            if (reader.UInt32("RecordLength") != data.Length)
                BinaryFormatReader.Fail(
                    "RecordLength",
                    "The declared facial record length differs from the supplied buffer.",
                    8,
                    4
                );
            var count = reader.UInt16("ImageCount");
            if (count == 0)
                BinaryFormatReader.Fail(
                    "ImageCount",
                    "A facial record must contain at least one image.",
                    12,
                    2
                );
            var images = ImmutableArray.CreateBuilder<FaceImage>(count);
            for (var i = 0; i < count; i++)
            {
                var path = $"Images[{i}]";
                var origin = reader.Offset;
                var blockLength = reader.UInt32(path + ".Length");
                if (blockLength < 33 || blockLength - 4 > reader.Remaining)
                    BinaryFormatReader.Fail(
                        "FaceBlockLength",
                        "The facial block length is too short or extends beyond its record.",
                        origin,
                        4
                    );
                var block = reader.Read(checked((int)blockLength - 4), path + ".Block");
                var faceReader = new BinaryFormatReader(block, origin + 4, fields, options);
                var featureCount = faceReader.UInt16(path + ".FeatureCount");
                var gender = faceReader.Byte(path + ".Gender");
                var eye = faceReader.Byte(path + ".EyeColor");
                var hair = faceReader.Byte(path + ".HairColor");
                var mask = faceReader.UInt24(path + ".FeatureMask");
                var expression = faceReader.UInt16(path + ".Expression");
                var pose = faceReader.UInt24(path + ".PoseAngle");
                var uncertainty = faceReader.UInt24(path + ".PoseUncertainty");
                if (8L * featureCount + 13 > faceReader.Remaining)
                    BinaryFormatReader.Fail(
                        "Truncated",
                        "The declared feature points leave insufficient bytes for image information and encoded image data.",
                        origin + 4,
                        2
                    );
                var points = ImmutableArray.CreateBuilder<FaceFeaturePoint>(featureCount);
                for (var p = 0; p < featureCount; p++)
                {
                    var prefix = path + $".FeaturePoints[{p}]";
                    var type = faceReader.Byte(prefix + ".Type");
                    var identifier = faceReader.Byte(prefix + ".Identifier");
                    var x = faceReader.UInt16(prefix + ".X");
                    var y = faceReader.UInt16(prefix + ".Y");
                    if (faceReader.UInt16(prefix + ".Reserved") != 0)
                        BinaryFormatReader.Fail(
                            "ReservedBits",
                            "Reserved feature-point bytes must be zero.",
                            faceReader.Offset - 2,
                            2
                        );
                    points.Add(new(type, identifier, x, y));
                }
                var imageType = faceReader.Byte(path + ".ImageType");
                var dataType = faceReader.Byte(path + ".ImageDataType");
                var width = faceReader.UInt16(path + ".Width");
                var height = faceReader.UInt16(path + ".Height");
                var color = faceReader.Byte(path + ".ColorSpace");
                var source = faceReader.Byte(path + ".SourceType");
                var device = faceReader.UInt16(path + ".DeviceType");
                var quality = faceReader.UInt16(path + ".Quality");
                if (width == 0 || height == 0 || dataType > 1)
                    BinaryFormatReader.Fail(
                        "FaceImageInformation",
                        "Image dimensions must be positive and image encoding must be JPEG or JPEG 2000.",
                        faceReader.Offset - 12,
                        12
                    );
                if (faceReader.Remaining == 0)
                    BinaryFormatReader.Fail(
                        "EmptyImage",
                        "A facial block must contain encoded image bytes.",
                        faceReader.Offset,
                        0
                    );
                var image = faceReader.Bytes(faceReader.Remaining, path + ".ImageData");
                images.Add(
                    new(
                        new(gender, eye, hair, mask, expression, pose, uncertainty),
                        points.MoveToImmutable(),
                        new(imageType, dataType, width, height, color, source, device, quality),
                        image
                    )
                );
            }
            reader.RequireEnd();
            return new(new(images.MoveToImmutable()), fields, []);
        }
        catch (FormatReadException error)
        {
            return new(null, fields, [error.Diagnostic]);
        }
    }

    /// <summary>Serializes every image block, deriving counts and lengths from the immutable data.</summary>
    /// <param name="record">The facial interchange record.</param>
    /// <returns>Fresh FAC bytes owned by the caller.</returns>
    /// <exception cref="ArgumentException">The record cannot be represented by INCITS 385-2004.</exception>
    public static byte[] Write(FaceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Images.IsDefaultOrEmpty || record.Images.Length > ushort.MaxValue)
            throw new ArgumentException(
                "A facial record requires between one and 65535 image blocks.",
                nameof(record)
            );
        using var body = new MemoryStream();
        foreach (var image in record.Images)
        {
            if (
                image is null
                || image.Information is null
                || image.ImageInformation is null
                || image.FeaturePoints.IsDefault
                || image.FeaturePoints.Length > ushort.MaxValue
                || image.ImageData.IsDefaultOrEmpty
            )
                throw new ArgumentException(
                    "A facial block has missing or oversized fields.",
                    nameof(record)
                );
            var info = image.Information;
            var encoding = image.ImageInformation;
            BinaryFormatWriter.UInt32(
                body,
                checked((uint)(32L + 8L * image.FeaturePoints.Length + image.ImageData.Length))
            );
            BinaryFormatWriter.UInt16(body, (ushort)image.FeaturePoints.Length);
            body.WriteByte(info.Gender);
            body.WriteByte(info.EyeColor);
            body.WriteByte(info.HairColor);
            BinaryFormatWriter.UInt24(body, info.FeatureMask);
            BinaryFormatWriter.UInt16(body, info.Expression);
            BinaryFormatWriter.UInt24(body, info.PoseAngle);
            BinaryFormatWriter.UInt24(body, info.PoseUncertainty);
            foreach (var point in image.FeaturePoints)
            {
                if (point is null)
                    throw new ArgumentException("A feature point is missing.", nameof(record));
                body.WriteByte(point.Type);
                body.WriteByte(point.Identifier);
                BinaryFormatWriter.UInt16(body, point.X);
                BinaryFormatWriter.UInt16(body, point.Y);
                BinaryFormatWriter.UInt16(body, 0);
            }
            body.WriteByte(encoding.FaceImageType);
            body.WriteByte(encoding.ImageDataType);
            BinaryFormatWriter.UInt16(body, encoding.Width);
            BinaryFormatWriter.UInt16(body, encoding.Height);
            body.WriteByte(encoding.ColorSpace);
            body.WriteByte(encoding.SourceType);
            BinaryFormatWriter.UInt16(body, encoding.DeviceType);
            // Quality is a two-byte field. Writing it intact preserves sentinel values.
            BinaryFormatWriter.UInt16(body, encoding.Quality);
            body.Write(image.ImageData.AsSpan());
        }
        using var result = new MemoryStream();
        result.Write("FAC\0010\0"u8);
        BinaryFormatWriter.UInt32(result, checked((uint)(14 + body.Length)));
        BinaryFormatWriter.UInt16(result, (ushort)record.Images.Length);
        body.Position = 0;
        body.CopyTo(result);
        var bytes = result.ToArray();
        var validation = Parse(bytes);
        if (!validation.IsSuccess)
            throw new ArgumentException(validation.Diagnostics[0].Message, nameof(record));
        return bytes;
    }
}
