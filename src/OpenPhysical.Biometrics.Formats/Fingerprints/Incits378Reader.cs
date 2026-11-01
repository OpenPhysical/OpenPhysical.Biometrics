// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Reads the record framing and general header, then delegates each finger view.
internal static class Incits378Reader
{
    private static readonly byte[] Identifier = "FMR\0"u8.ToArray();
    private static readonly byte[] Version = " 20\0"u8.ToArray();

    internal static Result<FingerprintRecord, FormatError> ReadRecord(
        BinaryFormatReader reader,
        int totalLength
    ) =>
        reader
            .Expect(
                "FormatIdentifier",
                Identifier,
                "FormatIdentifier",
                "Expected an INCITS 378 fingerprint record.",
                0
            )
            .Bind(() =>
                reader.Expect(
                    "Version",
                    Version,
                    "UnsupportedVersion",
                    "The fingerprint record version is unsupported.",
                    4
                )
            )
            .Bind(() => ReadRecordLength(reader, totalLength))
            .Bind(() => ReadHeader(reader))
            .Bind(header =>
                Incits378ViewReader.ReadViews(reader, header).Map(views => header.ToRecord(views))
            )
            .Check(_ => reader.RequireEnd());

    private static UnitResult<FormatError> ReadRecordLength(
        BinaryFormatReader reader,
        int totalLength
    ) =>
        reader
            .UInt16("RecordLength")
            .Bind(shortLength => shortLength == 0 ? ReadExtendedLength(reader) : shortLength)
            .Bind(length =>
                reader.Require(
                    length == totalLength,
                    "RecordLength",
                    "Declared record length differs from the supplied buffer length.",
                    8,
                    reader.Position - 8
                )
            );

    private static Result<uint, FormatError> ReadExtendedLength(BinaryFormatReader reader) =>
        reader
            .UInt32("ExtendedRecordLength")
            .Ensure(
                length => length > ushort.MaxValue,
                _ =>
                    reader.Error(
                        "RecordLength",
                        "The extended length encoding requires a record longer than 65535 bytes.",
                        8,
                        6
                    )
            );

    private static Result<Incits378Header, FormatError> ReadHeader(BinaryFormatReader reader) =>
        reader
            .UInt16s(
                "ProductOwner",
                "ProductType",
                "CaptureEquipment",
                "Width",
                "Height",
                "XResolution",
                "YResolution"
            )
            .Check(v =>
                reader.Require(
                    v[3] != 0 && v[4] != 0 && v[5] != 0 && v[6] != 0,
                    "Geometry",
                    "Image dimensions and resolutions must be positive.",
                    reader.Offset - 8,
                    8
                )
            )
            .Bind(v => ReadViewCount(reader).Map(count => ToHeader(v, count)))
            .Check(_ => ReadReserved(reader));

    private static Result<byte, FormatError> ReadViewCount(BinaryFormatReader reader) =>
        reader
            .Byte("ViewCount")
            .Ensure(
                count => count != 0,
                _ =>
                    reader.Error(
                        "ViewCount",
                        "A fingerprint record must contain at least one view.",
                        reader.Offset - 1,
                        1
                    )
            );

    private static UnitResult<FormatError> ReadReserved(BinaryFormatReader reader) =>
        reader
            .Byte("Reserved")
            .Bind(value =>
                reader.Require(
                    value == 0,
                    "ReservedBits",
                    "The reserved header byte must be zero.",
                    reader.Offset - 1,
                    1
                )
            );

    private static Incits378Header ToHeader(ImmutableArray<ushort> v, byte count) =>
        new(v[0], v[1], v[2], v[3], v[4], v[5], v[6], count);
}
