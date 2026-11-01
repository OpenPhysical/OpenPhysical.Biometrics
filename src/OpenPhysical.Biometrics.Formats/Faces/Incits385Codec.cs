// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Faces;

using CSharpFunctionalExtensions;

/// <summary>Reads and writes complete INCITS 385-2004 facial records, including multiple image blocks.</summary>
public static class Incits385Codec
{
    /// <summary>Parses one complete facial interchange record with bounded image blocks.</summary>
    /// <param name="data">Raw FAC record bytes.</param>
    /// <returns>An immutable record with field locations, or located structural diagnostics.</returns>
    public static Result<ParsedRecord<FaceRecord>, FormatError> Parse(ReadOnlySpan<byte> data) =>
        Parse(data, new InspectionOptions());

    /// <summary>Parses one complete facial interchange record with bounded image blocks.</summary>
    /// <param name="data">Raw FAC record bytes.</param>
    /// <param name="options">Explicit opt-in for sensitive values in inspection output.</param>
    /// <returns>An immutable record with field locations, or located structural diagnostics.</returns>
    public static Result<ParsedRecord<FaceRecord>, FormatError> Parse(
        ReadOnlySpan<byte> data,
        InspectionOptions options
    )
    {
        var reader = BinaryFormatReader.Create(data, options);
        return Incits385Reader
            .ReadRecord(reader, data.Length)
            .Map(record => new ParsedRecord<FaceRecord>(record, reader.Report()));
    }

    /// <summary>Serializes every image block, deriving counts and lengths from the immutable data.</summary>
    /// <param name="record">The facial interchange record.</param>
    /// <returns>Fresh FAC bytes owned by the caller, or an error when a field exceeds the INCITS 385-2004 encoding limits.</returns>
    public static Result<byte[], FormatError> Write(FaceRecord record) =>
        Incits385Validator
            .Validate(record)
            .Map(() => Incits385Writer.Serialize(record))
            .Bind(bytes => Parse(bytes).Map(_ => bytes));
}
