namespace OpenPhysical.Domains.Cbeff.Formats.Incits385;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;

/// <summary>
/// Represents the header of an INCITS 385 facial image record.
/// </summary>
public sealed record FacialRecordHeader
{
    /// <summary>
    /// Gets the format identifier (should be "FAC\0" = 0x46414300).
    /// </summary>
    public string FormatIdentifier { get; }

    /// <summary>
    /// Gets the version number (should be "010\0" = 0x30313000).
    /// </summary>
    public string VersionNumber { get; }

    /// <summary>
    /// Gets the length of the entire facial record in bytes.
    /// </summary>
    public uint RecordLength { get; }

    /// <summary>
    /// Gets the number of facial images in this record.
    /// </summary>
    public ushort NumberOfFaces { get; }

    private FacialRecordHeader(
        string formatIdentifier,
        string versionNumber,
        uint recordLength,
        ushort numberOfFaces
    )
    {
        FormatIdentifier = formatIdentifier;
        VersionNumber = versionNumber;
        RecordLength = recordLength;
        NumberOfFaces = numberOfFaces;
    }

    /// <summary>
    /// Creates a new facial record header with validation.
    /// </summary>
    public static Result<FacialRecordHeader> Create(
        string formatIdentifier,
        string versionNumber,
        uint recordLength,
        ushort numberOfFaces
    )
    {
        const string ExpectedFormatId = "FAC\0";
        const string ExpectedVersion = "010\0";

        if (formatIdentifier != ExpectedFormatId)
            return Result.Failure<FacialRecordHeader>(
                $"Invalid format identifier: \"{EscapeString(formatIdentifier)}\", expected \"{EscapeString(ExpectedFormatId)}\""
            );

        if (versionNumber != ExpectedVersion)
            return Result.Failure<FacialRecordHeader>(
                $"Invalid version number: \"{EscapeString(versionNumber)}\", expected \"{EscapeString(ExpectedVersion)}\""
            );

        if (recordLength < 14) // Minimum size for header alone
            return Result.Failure<FacialRecordHeader>(
                $"Invalid record length: {recordLength}, must be at least 14 bytes"
            );

        if (numberOfFaces == 0)
            return Result.Failure<FacialRecordHeader>("Number of faces must be at least 1");

        return Result.Success(
            new FacialRecordHeader(formatIdentifier, versionNumber, recordLength, numberOfFaces)
        );
    }

    /// <summary>
    /// Creates a new facial record header from raw bytes with validation.
    /// </summary>
    public static Result<FacialRecordHeader> CreateFromBytes(
        uint formatIdBytes,
        uint versionBytes,
        uint recordLength,
        ushort numberOfFaces
    )
    {
        // Convert bytes to ASCII strings
        var formatId = System.Text.Encoding.ASCII.GetString(
            new[]
            {
                (byte)(formatIdBytes >> 24),
                (byte)(formatIdBytes >> 16),
                (byte)(formatIdBytes >> 8),
                (byte)formatIdBytes,
            }
        );

        var version = System.Text.Encoding.ASCII.GetString(
            new[]
            {
                (byte)(versionBytes >> 24),
                (byte)(versionBytes >> 16),
                (byte)(versionBytes >> 8),
                (byte)versionBytes,
            }
        );

        return Create(formatId, version, recordLength, numberOfFaces);
    }

    private static string EscapeString(string value) => value.Replace("\0", "\\0");
}
