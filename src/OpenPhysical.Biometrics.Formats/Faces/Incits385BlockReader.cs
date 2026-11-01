// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Faces;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Reads the contents of one bounded facial image block. A feature count can never borrow
// bytes from the next block because the reader ends at the block boundary.
internal static class Incits385BlockReader
{
    internal static Result<FaceImage, FormatError> Read(
        BinaryFormatReader block,
        string path,
        int blockOrigin
    ) =>
        from head in ReadInformation(block, path, blockOrigin)
        from points in ReadPoints(block, path, head.Count)
        from encoding in ReadEncoding(block, path)
        from image in ReadImageData(block, path)
        select new FaceImage(head.Information, points, encoding, image);

    private static Result<(ushort Count, FaceInformation Information), FormatError> ReadInformation(
        BinaryFormatReader block,
        string path,
        int blockOrigin
    ) =>
        (
            from count in block.UInt16(path + ".FeatureCount")
            from gender in block.Byte(path + ".Gender")
            from eye in block.Byte(path + ".EyeColor")
            from hair in block.Byte(path + ".HairColor")
            from mask in block.UInt24(path + ".FeatureMask")
            from expression in block.UInt16(path + ".Expression")
            from pose in block.UInt24(path + ".PoseAngle")
            from uncertainty in block.UInt24(path + ".PoseUncertainty")
            select (
                count,
                new FaceInformation(gender, eye, hair, mask, expression, pose, uncertainty)
            )
        ).Check(head =>
            block.Require(
                8L * head.count + 13 <= block.Remaining,
                "Truncated",
                "The declared feature points leave insufficient bytes for image information and encoded image data.",
                blockOrigin,
                2
            )
        );

    private static Result<ImmutableArray<FaceFeaturePoint>, FormatError> ReadPoints(
        BinaryFormatReader block,
        string path,
        int count
    ) =>
        ParseSteps.Repeat<FaceFeaturePoint>(
            count,
            done => ReadPoint(block, $"{path}.FeaturePoints[{done.Count}]")
        );

    private static Result<FaceFeaturePoint, FormatError> ReadPoint(
        BinaryFormatReader block,
        string prefix
    ) =>
        (
            from type in block.Byte(prefix + ".Type")
            from identifier in block.Byte(prefix + ".Identifier")
            from x in block.UInt16(prefix + ".X")
            from y in block.UInt16(prefix + ".Y")
            from reserved in block.UInt16(prefix + ".Reserved")
            select (point: new FaceFeaturePoint(type, identifier, x, y), reserved)
        )
            .Check(item =>
                block.Require(
                    item.reserved == 0,
                    "ReservedBits",
                    "Reserved feature-point bytes must be zero.",
                    block.Offset - 2,
                    2
                )
            )
            .Map(item => item.point);

    private static Result<FaceImageInformation, FormatError> ReadEncoding(
        BinaryFormatReader block,
        string path
    ) =>
        (
            from imageType in block.Byte(path + ".ImageType")
            from dataType in block.Byte(path + ".ImageDataType")
            from width in block.UInt16(path + ".Width")
            from height in block.UInt16(path + ".Height")
            from color in block.Byte(path + ".ColorSpace")
            from source in block.Byte(path + ".SourceType")
            from device in block.UInt16(path + ".DeviceType")
            from quality in block.UInt16(path + ".Quality")
            select new FaceImageInformation(
                imageType,
                dataType,
                width,
                height,
                color,
                source,
                device,
                quality
            )
        ).Check(info =>
            block.Require(
                info.Width != 0 && info.Height != 0 && info.ImageDataType <= 1,
                "FaceImageInformation",
                "Image dimensions must be positive and image encoding must be JPEG or JPEG 2000.",
                block.Offset - 12,
                12
            )
        );

    private static Result<ImmutableArray<byte>, FormatError> ReadImageData(
        BinaryFormatReader block,
        string path
    ) =>
        block
            .Require(
                block.Remaining != 0,
                "EmptyImage",
                "A facial block must contain encoded image bytes.",
                block.Offset,
                0
            )
            .Bind(() => block.Bytes(block.Remaining, path + ".ImageData"));
}
