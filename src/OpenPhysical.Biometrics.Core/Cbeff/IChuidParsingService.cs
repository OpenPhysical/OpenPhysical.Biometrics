namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using Org.BouncyCastle.X509;
using System;

/// <summary>
/// Service for parsing CHUID (Card Holder Unique Identifier) structures.
/// Per FIPS 201-3, CHUID contains PIV card holder information and signing certificate.
/// </summary>
public interface IChuidParsingService
{
    /// <summary>
    /// Parses a CHUID structure from raw bytes.
    /// </summary>
    /// <param name="chuidData">The raw CHUID data (typically from 5FC1A4.dat file)</param>
    /// <returns>Result containing parsed CHUID structure or error</returns>
    Result<ChuidStructure> ParseChuidStructure(byte[] chuidData);

    /// <summary>
    /// Extracts the signing certificate from CHUID asymmetric signature.
    /// </summary>
    /// <param name="signatureData">The asymmetric signature data from CHUID</param>
    /// <returns>Result containing the signing certificate or error</returns>
    Result<X509Certificate> ExtractSigningCertificate(byte[] signatureData);
}

/// <summary>
/// Represents the parsed structure of a CHUID.
/// Per SP 800-73-5 Table 9, CHUID contains multiple TLV fields.
/// </summary>
/// <param name="FascN">Federal Agency Smart Credential Number (tag 0x30)</param>
/// <param name="CardUuid">Card UUID (tag 0x34)</param>
/// <param name="ExpirationDate">Expiration date (tag 0x35)</param>
/// <param name="AsymmetricSignature">CMS signature (tag 0x3E)</param>
/// <param name="LrcSignature">Error detection code (tag 0xFE)</param>
public sealed record ChuidStructure(
    byte[]? FascN,
    Guid? CardUuid,
    DateTime? ExpirationDate,
    byte[]? AsymmetricSignature,
    byte[]? LrcSignature);