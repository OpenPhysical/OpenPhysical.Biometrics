// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Faces;

// Serializes a validated facial record. Counts and lengths derive from the immutable data.
internal static class Incits385Writer
{
    private static readonly byte[] Signature = "FAC\0010\0"u8.ToArray();

    internal static byte[] Serialize(FaceRecord record)
    {
        var body = record.Images.Aggregate(new BinaryFormatWriter(), WriteImage);
        return new BinaryFormatWriter()
            .Bytes(Signature)
            .UInt32((uint)(14 + body.Length))
            .UInt16((ushort)record.Images.Length)
            .Bytes(body.ToArray())
            .ToArray();
    }

    private static BinaryFormatWriter WriteImage(BinaryFormatWriter body, FaceImage image)
    {
        var info = image.Information;
        var header = body.UInt32((uint)Incits385Validator.BlockLength(image))
            .UInt16((ushort)image.FeaturePoints.Length)
            .Byte(info.Gender)
            .Byte(info.EyeColor)
            .Byte(info.HairColor)
            .UInt24(info.FeatureMask)
            .UInt16(info.Expression)
            .UInt24(info.PoseAngle)
            .UInt24(info.PoseUncertainty);
        var withPoints = image.FeaturePoints.Aggregate(
            header,
            (writer, point) =>
                writer
                    .Byte(point.Type)
                    .Byte(point.Identifier)
                    .UInt16(point.X)
                    .UInt16(point.Y)
                    .UInt16(0)
        );
        return WriteEncoding(withPoints, image.ImageInformation).Bytes(image.ImageData);
    }

    // Quality is a two-byte field. Writing it intact preserves sentinel values.
    private static BinaryFormatWriter WriteEncoding(
        BinaryFormatWriter writer,
        FaceImageInformation encoding
    ) =>
        writer
            .Byte(encoding.FaceImageType)
            .Byte(encoding.ImageDataType)
            .UInt16(encoding.Width)
            .UInt16(encoding.Height)
            .Byte(encoding.ColorSpace)
            .Byte(encoding.SourceType)
            .UInt16(encoding.DeviceType)
            .UInt16(encoding.Quality);
}
