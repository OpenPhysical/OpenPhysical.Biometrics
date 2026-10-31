namespace OpenPhysical.Domains.Cbeff;

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
/// Represents an immutable Common Biometric Exchange Formats Framework (CBEFF) header.
/// </summary>
/// <remarks>
/// The CBEFF header contains metadata about biometric data including format information,
/// security options, and data lengths. This implementation follows NIST SP 800-76-2.
/// </remarks>
public sealed record CbeffHeader
{
    /// <summary>
    /// Standard CBEFF header size in bytes.
    /// </summary>
    public const int StandardHeaderSize = 88;

    /// <summary>
    /// Gets the patron header version.
    /// </summary>
    public byte PatronHeaderVersion { get; }

    /// <summary>
    /// Gets the SBH (Standard Biometric Header) security options.
    /// </summary>
    public byte SbhSecurityOptions { get; }

    /// <summary>
    /// Gets the length of the BDB (Biometric Data Block) in bytes.
    /// </summary>
    public uint BdbLength { get; }

    /// <summary>
    /// Gets the length of the SB (Signature Block) in bytes.
    /// </summary>
    public ushort SbLength { get; }

    /// <summary>
    /// Gets the BDB format owner identifier.
    /// </summary>
    public ushort BdbFormatOwner { get; }

    /// <summary>
    /// Gets the BDB format type identifier.
    /// </summary>
    public ushort BdbFormatType { get; }

    /// <summary>
    /// Gets the creation date of the biometric data.
    /// </summary>
    public DateTime CreationDate { get; }

    /// <summary>
    /// Gets the validity period start date.
    /// </summary>
    public DateTime ValidityNotBefore { get; }

    /// <summary>
    /// Gets the validity period end date.
    /// </summary>
    public DateTime ValidityNotAfter { get; }

    /// <summary>
    /// Gets the biometric type (modality).
    /// </summary>
    public uint BiometricType { get; }

    /// <summary>
    /// Gets the biometric subtype.
    /// </summary>
    public byte BiometricSubtype { get; }

    /// <summary>
    /// Gets the biometric quality score (-2 to 100, -1 = not supported).
    /// </summary>
    public sbyte Quality { get; }

    /// <summary>
    /// Gets the creator field (18 bytes).
    /// </summary>
    public byte[] Creator { get; }

    /// <summary>
    /// Gets the FASC-N (Federal Agency Smart Credential Number).
    /// </summary>
    public byte[] FascN { get; }

    /// <summary>
    /// Gets reserved bytes for future use.
    /// </summary>
    public byte[] Reserved { get; }

    /// <summary>
    /// Initializes a new instance of the CbeffHeader record.
    /// </summary>
    private CbeffHeader(
        byte patronHeaderVersion,
        byte sbhSecurityOptions,
        uint bdbLength,
        ushort sbLength,
        ushort bdbFormatOwner,
        ushort bdbFormatType,
        DateTime creationDate,
        DateTime validityNotBefore,
        DateTime validityNotAfter,
        uint biometricType,
        byte biometricSubtype,
        sbyte quality,
        byte[] creator,
        byte[] fascN,
        byte[] reserved
    )
    {
        PatronHeaderVersion = patronHeaderVersion;
        SbhSecurityOptions = sbhSecurityOptions;
        BdbLength = bdbLength;
        SbLength = sbLength;
        BdbFormatOwner = bdbFormatOwner;
        BdbFormatType = bdbFormatType;
        CreationDate = creationDate;
        ValidityNotBefore = validityNotBefore;
        ValidityNotAfter = validityNotAfter;
        BiometricType = biometricType;
        BiometricSubtype = biometricSubtype;
        Quality = quality;
        Creator = creator;
        FascN = fascN;
        Reserved = reserved;
    }

    /// <summary>
    /// Creates a CBEFF header with the specified values.
    /// </summary>
    /// <returns>A Result containing the CbeffHeader or an error.</returns>
    public static Result<CbeffHeader> Create(
        byte patronHeaderVersion,
        byte sbhSecurityOptions,
        uint bdbLength,
        ushort sbLength,
        ushort bdbFormatOwner,
        ushort bdbFormatType,
        DateTime creationDate,
        DateTime validityNotBefore,
        DateTime validityNotAfter,
        uint biometricType,
        byte biometricSubtype,
        sbyte quality,
        byte[] creator,
        byte[] fascN,
        byte[] reserved
    ) =>
        Result
            .Try(
                () =>
                {
                    if (creator.Length != 18)
                        return Result.Failure<CbeffHeader>("Creator must be exactly 18 bytes");

                    if (fascN.Length != 25)
                        return Result.Failure<CbeffHeader>("FASC-N must be exactly 25 bytes");

                    if (reserved.Length != 4)
                        return Result.Failure<CbeffHeader>("Reserved must be exactly 4 bytes");

                    return Result.Success(
                        new CbeffHeader(
                            patronHeaderVersion,
                            sbhSecurityOptions,
                            bdbLength,
                            sbLength,
                            bdbFormatOwner,
                            bdbFormatType,
                            creationDate,
                            validityNotBefore,
                            validityNotAfter,
                            biometricType,
                            biometricSubtype,
                            quality,
                            (byte[])creator.Clone(),
                            (byte[])fascN.Clone(),
                            (byte[])reserved.Clone()
                        )
                    );
                },
                ex => $"Invalid header data: {ex.Message}"
            )
            .Bind(result => result);

    /// <summary>
    /// Gets a copy of the creator field to maintain immutability.
    /// </summary>
    /// <returns>A copy of the creator bytes</returns>
    public byte[] GetCreator() => (byte[])Creator.Clone();

    /// <summary>
    /// Gets a copy of the FASC-N to maintain immutability.
    /// </summary>
    /// <returns>A copy of the FASC-N bytes</returns>
    public byte[] GetFascN() => (byte[])FascN.Clone();

    /// <summary>
    /// Gets a copy of the reserved field to maintain immutability.
    /// </summary>
    /// <returns>A copy of the reserved bytes</returns>
    public byte[] GetReserved() => (byte[])Reserved.Clone();
}
