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
/// Metadata required for creating CBEFF objects.
/// This is an immutable value object containing the administrative data
/// needed to construct CBEFF headers per SP 800-76-2.
/// </summary>
/// <param name="CreatorName">Name of the entity creating the biometric data (max 18 bytes)</param>
/// <param name="FascN">Federal Agency Smart Credential Number (25 bytes)</param>
/// <param name="CreationDate">Date when the biometric data was created</param>
/// <param name="ValidityStartDate">Start of validity period</param>
/// <param name="ValidityEndDate">End of validity period</param>
/// <param name="BiometricQuality">Quality score (-2 to 100, -1 = not supported, 254/255 = unusable)</param>
/// <param name="PatronHeaderVersion">Patron header version (default 0x03 for PIV)</param>
/// <param name="SecurityOptions">Security options (0x08 = integrity, 0x0D = signed)</param>
public sealed record CbeffMetadata(
    string CreatorName,
    byte[] FascN,
    DateTime CreationDate,
    DateTime ValidityStartDate,
    DateTime ValidityEndDate,
    sbyte BiometricQuality = -1,
    byte PatronHeaderVersion = 0x03,
    byte SecurityOptions = 0x0D
)
{
    /// <summary>
    /// Creates CbeffMetadata with validation.
    /// </summary>
    public static Result<CbeffMetadata> Create(
        string creatorName,
        byte[] fascN,
        DateTime creationDate,
        DateTime validityStartDate,
        DateTime validityEndDate,
        sbyte biometricQuality = -1
    )
    {
        if (string.IsNullOrWhiteSpace(creatorName))
            return Result.Failure<CbeffMetadata>("Creator name cannot be empty");

        if (creatorName.Length > 18)
            return Result.Failure<CbeffMetadata>("Creator name cannot exceed 18 characters");

        if (fascN == null || fascN.Length != 25)
            return Result.Failure<CbeffMetadata>("FASC-N must be exactly 25 bytes");

        if (validityEndDate <= validityStartDate)
            return Result.Failure<CbeffMetadata>("Validity end date must be after start date");

        // Valid ranges: -2 to 100, or special PIV values -2 (254 as byte) and -1 (255 as byte)
        if (biometricQuality < -2 || biometricQuality > 100)
            return Result.Failure<CbeffMetadata>("Invalid biometric quality value");

        return Result.Success(
            new CbeffMetadata(
                creatorName,
                (byte[])fascN.Clone(),
                creationDate,
                validityStartDate,
                validityEndDate,
                biometricQuality
            )
        );
    }

    /// <summary>
    /// Creates metadata for an unusable biometric (fingerprint that cannot be captured).
    /// </summary>
    public static CbeffMetadata CreateUnusable(
        string creatorName,
        byte[] fascN,
        DateTime expirationDate,
        bool isTemporary
    )
    {
        var now = DateTime.UtcNow;
        var quality = (sbyte)(isTemporary ? -1 : -2); // -1 = 255 as byte, -2 = 254 as byte

        return new CbeffMetadata(creatorName, fascN, now, now, expirationDate, quality);
    }

    /// <summary>
    /// Creates default metadata for PIV use.
    /// </summary>
    public static Result<CbeffMetadata> CreateForPiv(
        string creatorName,
        byte[] fascN,
        DateTime validityEndDate,
        sbyte quality = -1
    )
    {
        var now = DateTime.UtcNow;
        return Create(creatorName, fascN, now, now, validityEndDate, quality);
    }

    /// <summary>
    /// Validates that this metadata is suitable for PIV use.
    /// </summary>
    public Result ValidateForPiv()
    {
        if (PatronHeaderVersion != 0x03)
            return Result.Failure("PIV requires patron header version 0x03");

        if (SecurityOptions != 0x08 && SecurityOptions != 0x0C && SecurityOptions != 0x0D)
            return Result.Failure("PIV requires security options 0x08, 0x0C, or 0x0D");

        if (FascN == null || FascN.Length != 25)
            return Result.Failure("PIV requires a 25-byte FASC-N");

        return Result.Success();
    }

    /// <summary>
    /// Gets a copy of the FASC-N to maintain immutability.
    /// </summary>
    public byte[] GetFascN() => (byte[])FascN.Clone();
}
