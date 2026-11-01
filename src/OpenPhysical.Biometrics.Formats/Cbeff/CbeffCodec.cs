// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Cbeff;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Reads and writes PIV CBEFF records while preserving the exact signed header and biometric bytes.</summary>
public static class CbeffCodec
{
    /// <summary>Parses a raw CBEFF record containing exactly one header, biometric block, and security block.</summary>
    /// <param name="data">Unwrapped CBEFF bytes.</param>
    /// <returns>The parsed record with field locations, or located structural errors.</returns>
    public static Result<ParsedRecord<CbeffRecord>, FormatError> Parse(ReadOnlySpan<byte> data) =>
        Parse(data, new InspectionOptions());

    /// <summary>Parses a raw CBEFF record containing exactly one header, biometric block, and security block.</summary>
    /// <param name="data">Unwrapped CBEFF bytes.</param>
    /// <param name="options">Explicit opt-in for sensitive inspection values.</param>
    /// <returns>The parsed record with field locations, or located structural errors.</returns>
    public static Result<ParsedRecord<CbeffRecord>, FormatError> Parse(
        ReadOnlySpan<byte> data,
        InspectionOptions options
    )
    {
        var reader = BinaryFormatReader.Create(data, options);
        var length = data.Length;
        return reader
            .Read(CbeffHeader.Size, "Header")
            .Bind(raw => CbeffHeaderReader.Read(reader.Nested(raw, 0), raw))
            .Bind(header => CheckLength(reader, header, length))
            .Bind(header => ReadBlocks(reader, header))
            .Map(record => new ParsedRecord<CbeffRecord>(record, reader.Report()));
    }

    /// <summary>Writes a record whose header lengths already match its contents.</summary>
    /// <param name="record">The complete record to serialize.</param>
    /// <returns>Fresh CBEFF bytes owned by the caller, or an error when declared lengths differ from the immutable blocks.</returns>
    public static Result<byte[], FormatError> Write(CbeffRecord record) =>
        Validate(record, requireSignature: true).Map(() => Assemble(record, record.Signature));

    /// <summary>Returns the exact header followed by biometric data for detached CMS processing.</summary>
    /// <param name="record">The record whose lengths have been finalized before signing.</param>
    /// <returns>The exact header and biometric-record bytes covered by the signature, or an error when the biometric block length differs from the header.</returns>
    public static Result<byte[], FormatError> GetSignedData(CbeffRecord record) =>
        Validate(record, requireSignature: false)
            .Map(() => Assemble(record, ImmutableArray<byte>.Empty));

    private static Result<CbeffHeader, FormatError> CheckLength(
        BinaryFormatReader reader,
        CbeffHeader header,
        int length
    ) =>
        reader
            .Require(
                (ulong)CbeffHeader.Size + header.BiometricDataLength + header.SignatureLength
                    == (ulong)length,
                "CbeffLength",
                "The declared biometric and security block lengths differ from the buffer length.",
                2,
                6
            )
            .Map(() => header);

    // The declared lengths were checked against the buffer, so each fits in an int.
    private static Result<CbeffRecord, FormatError> ReadBlocks(
        BinaryFormatReader reader,
        CbeffHeader header
    ) =>
        reader
            .Bytes((int)header.BiometricDataLength, "BiometricData")
            .Bind(biometric =>
                reader
                    .Bytes(header.SignatureLength, "SecurityBlock")
                    .Map(signature => new CbeffRecord(header, biometric, signature))
            )
            .Check(_ => reader.RequireEnd());

    private static UnitResult<FormatError> Validate(CbeffRecord record, bool requireSignature) =>
        record.BiometricData.IsDefault
        || record.Header.BiometricDataLength != record.BiometricData.Length
        || (
            requireSignature
            && (
                record.Signature.IsDefault
                || record.Header.SignatureLength != record.Signature.Length
            )
        )
            ? UnitResult.Failure(
                FormatError.Invalid(
                    "CbeffLength",
                    "The biometric or security block length differs from the header."
                )
            )
            : UnitResult.Success<FormatError>();

    private static byte[] Assemble(CbeffRecord record, ImmutableArray<byte> signature) =>
        new BinaryFormatWriter()
            .Bytes(record.Header.RawBytes)
            .Bytes(record.BiometricData)
            .Bytes(signature)
            .ToArray();
}
