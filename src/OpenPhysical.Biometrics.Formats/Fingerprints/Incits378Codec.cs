// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using CSharpFunctionalExtensions;

/// <summary>Reads and writes complete INCITS 378-2004 records using bounded binary fields.</summary>
public static class Incits378Codec
{
    /// <summary>Parses one complete record and returns located diagnostics for malformed input.</summary>
    /// <param name="data">The raw FMR record bytes.</param>
    /// <returns>The immutable record and inspection report, or structural diagnostics.</returns>
    public static Result<ParsedRecord<FingerprintRecord>, FormatError> Parse(
        ReadOnlySpan<byte> data
    ) => Parse(data, new InspectionOptions());

    /// <summary>Parses one complete record and returns located diagnostics for malformed input.</summary>
    /// <param name="data">The raw FMR record bytes.</param>
    /// <param name="options">Explicit opt-in for sensitive values in the inspection report.</param>
    /// <returns>The immutable record and inspection report, or structural diagnostics.</returns>
    public static Result<ParsedRecord<FingerprintRecord>, FormatError> Parse(
        ReadOnlySpan<byte> data,
        InspectionOptions options
    )
    {
        var reader = BinaryFormatReader.Create(data, options);
        return Incits378Reader
            .ReadRecord(reader, data.Length)
            .Map(record => new ParsedRecord<FingerprintRecord>(record, reader.Report()));
    }

    /// <summary>Serializes a record, deriving all lengths and counts from its immutable collections.</summary>
    /// <param name="record">The record to write.</param>
    /// <returns>Complete FMR bytes owned by the caller, or an error when a field exceeds the INCITS 378-2004 encoding limits.</returns>
    public static Result<byte[], FormatError> Write(FingerprintRecord record) =>
        Incits378Validator
            .Validate(record)
            .Map(() => Incits378Writer.Serialize(record))
            .Bind(bytes => Parse(bytes).Map(_ => bytes));
}
