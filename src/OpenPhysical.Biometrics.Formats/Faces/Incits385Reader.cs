// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Faces;

using CSharpFunctionalExtensions;

// Reads the record framing, then each length-prefixed facial image block.
internal static class Incits385Reader
{
    private static readonly byte[] Identifier = "FAC\0"u8.ToArray();
    private static readonly byte[] Version = "010\0"u8.ToArray();

    internal static Result<FaceRecord, FormatError> ReadRecord(
        BinaryFormatReader reader,
        int totalLength
    ) =>
        reader
            .Expect(
                "FormatIdentifier",
                Identifier,
                "FormatIdentifier",
                "Expected an INCITS 385 facial record.",
                0
            )
            .Bind(() =>
                reader.Expect(
                    "Version",
                    Version,
                    "UnsupportedVersion",
                    "The facial record version is unsupported.",
                    4
                )
            )
            .Bind(() => reader.UInt32("RecordLength"))
            .Check(length =>
                reader.Require(
                    length == totalLength,
                    "RecordLength",
                    "The declared facial record length differs from the supplied buffer.",
                    8,
                    4
                )
            )
            .Bind(_ => ReadImageCount(reader))
            .Bind(count =>
                ParseSteps.Repeat<FaceImage>(count, done => ReadImage(reader, done.Count))
            )
            .Map(images => new FaceRecord(images))
            .Check(_ => reader.RequireEnd());

    private static Result<ushort, FormatError> ReadImageCount(BinaryFormatReader reader) =>
        reader
            .UInt16("ImageCount")
            .Ensure(
                count => count != 0,
                _ =>
                    reader.Error(
                        "ImageCount",
                        "A facial record must contain at least one image.",
                        12,
                        2
                    )
            );

    private static Result<FaceImage, FormatError> ReadImage(BinaryFormatReader reader, int index)
    {
        var path = $"Images[{index}]";
        var origin = reader.Offset;
        return reader
            .UInt32(path + ".Length")
            .Ensure(
                length => length >= 33 && length - 4 <= reader.Remaining,
                _ =>
                    reader.Error(
                        "FaceBlockLength",
                        "The facial block length is too short or extends beyond its record.",
                        origin,
                        4
                    )
            )
            .Bind(length => reader.Block((int)length - 4, path + ".Block"))
            .Bind(block => Incits385BlockReader.Read(block, path, origin + 4));
    }
}
