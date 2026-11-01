// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Cbeff;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Reads the fixed header field by field so every field has an inspection location.
internal static class CbeffHeaderReader
{
    private static readonly string[] DateNames =
    [
        "Header.CreationDate",
        "Header.ValidityNotBefore",
        "Header.ValidityNotAfter",
    ];

    internal static Result<CbeffHeader, FormatError> Read(
        BinaryFormatReader reader,
        ReadOnlyMemory<byte> raw
    ) =>
        reader
            .Byte("Header.PatronHeaderVersion")
            .Bind(_ => reader.Byte("Header.SecurityOptions"))
            .Bind(_ => reader.UInt32("Header.BiometricDataLength"))
            .Bind(_ => reader.UInt16("Header.SignatureLength"))
            .Bind(_ => reader.UInt16("Header.FormatOwner"))
            .Bind(_ => reader.UInt16("Header.FormatType"))
            .Bind(_ => ReadDates(reader))
            .Bind(_ => reader.UInt24("Header.BiometricType"))
            .Bind(_ => reader.Byte("Header.BiometricDataType"))
            .Bind(_ => reader.Byte("Header.Quality"))
            .Bind(_ => reader.Read(18, "Header.Creator"))
            .Bind(_ => reader.Read(25, "Header.FascN"))
            .Bind(_ => reader.Read(4, "Header.Reserved"))
            .Bind(_ =>
                CbeffHeader
                    .FromBytes(raw.Span)
                    .MapError(error => reader.Error(error.Code, error.Message, 0, raw.Length))
            );

    private static Result<ImmutableArray<DateTimeOffset>, FormatError> ReadDates(
        BinaryFormatReader reader
    ) => ParseSteps.Repeat<DateTimeOffset>(3, done => ReadDate(reader, DateNames[done.Count]));

    private static Result<DateTimeOffset, FormatError> ReadDate(
        BinaryFormatReader reader,
        string name
    ) =>
        reader
            .Read(8, name)
            .Bind(value =>
                CbeffTimestampCodec
                    .Decode(value.Span)
                    .MapError(_ =>
                        reader.Error(
                            CbeffTimestampCodec.InvalidTimestampCode,
                            "A header timestamp contains an invalid UTC calendar time.",
                            reader.Offset - 8,
                            8
                        )
                    )
            );
}
