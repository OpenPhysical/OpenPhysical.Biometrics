namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Service for parsing CHUID (Card Holder Unique Identifier) structures.
/// Implements FIPS 201-3 and SP 800-73-5 CHUID parsing requirements.
/// </summary>
public sealed class ChuidParsingService : IChuidParsingService
{
    private readonly ILogger<ChuidParsingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChuidParsingService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic logging.</param>
    public ChuidParsingService(ILogger<ChuidParsingService> logger)
    {
        _logger = logger;
    }
    /// <summary>
    /// Parses a CHUID structure from raw bytes.
    /// </summary>
    public Result<ChuidStructure> ParseChuidStructure(byte[] data)
    {
        try
        {
            // First, check if CHUID data is wrapped in PIV BC tag (0xBC)
            // PIV cards wrap CHUID data in BER/DER tag 0xBC (Biometric Information Template)
            var chuidData = data;
            if (BerTlv.HasBerTag(data, 0xBC))
            {
                var extractResult = BerTlv.ExtractValueIfTagged(data, 0xBC);
                if (extractResult.IsFailure)
                {
                    return Result.Failure<ChuidStructure>($"Failed to extract CHUID from BC tag: {extractResult.Error}");
                }
                chuidData = extractResult.Value;
            }

            // CHUID is a TLV structure per SP 800-73-5, Table 9
            var offset = 0;
            byte[]? fascN = null;
            Guid? cardUuid = null;
            DateTime? expirationDate = null;
            byte[]? asymmetricSignature = null;
            byte[]? lrcSignature = null;

            while (offset < chuidData.Length)
            {
                // Handle long form TLV encoding
                if (offset + 2 > chuidData.Length) break;

                var tag = chuidData[offset];
                var lengthByteCount = 1;
                var length = 0;

                // Parse length field (can be short form or long form)
                var lengthByte = chuidData[offset + 1];
                if ((lengthByte & 0x80) == 0)
                {
                    // Short form: length is in the single byte
                    length = lengthByte;
                    lengthByteCount = 1;
                }
                else
                {
                    // Long form: first byte indicates number of subsequent length bytes
                    var numLengthBytes = lengthByte & 0x7F;
                    if (numLengthBytes == 0 || numLengthBytes > 4)
                    {
                        return Result.Failure<ChuidStructure>($"Invalid long form length encoding: {numLengthBytes} bytes");
                    }

                    if (offset + 1 + numLengthBytes >= chuidData.Length)
                    {
                        return Result.Failure<ChuidStructure>("Truncated length field in CHUID TLV");
                    }

                    // Reconstruct length from multiple bytes (big-endian)
                    for (var i = 0; i < numLengthBytes; i++)
                    {
                        length = (length << 8) | chuidData[offset + 2 + i];
                    }
                    lengthByteCount = 1 + numLengthBytes;
                }

                offset += 1 + lengthByteCount; // Skip tag + length field(s)

                if (offset + length > chuidData.Length)
                {
                    return Result.Failure<ChuidStructure>($"Invalid TLV length in CHUID data: tag 0x{tag:X2}, length {length}, remaining data {chuidData.Length - offset}");
                }

                var value = new byte[length];
                Array.Copy(chuidData, offset, value, 0, length);
                offset += length;

                switch (tag)
                {
                    case 0x30: // FASC-N
                        fascN = value;
                        break;
                    case 0x34: // Card UUID
                        if (value.Length == 16)
                        {
                            cardUuid = new Guid(value);
                        }
                        break;
                    case 0x35: // Expiration Date
                        expirationDate = ParseChuidDate(value);
                        break;
                    case 0x3E: // Asymmetric Signature
                        asymmetricSignature = value;
                        _logger.LogDebug("Found CHUID asymmetric signature with {Length} bytes", value.Length);
                        break;
                    case 0xFE: // LRC (Error Detection Code)
                        lrcSignature = value;
                        break;
                    // Additional tags can be added as needed
                }
            }

            return Result.Success(new ChuidStructure(fascN, cardUuid, expirationDate, asymmetricSignature, lrcSignature));
        }
        catch (Exception ex)
        {
            return Result.Failure<ChuidStructure>($"Error parsing CHUID structure: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the signing certificate from CHUID asymmetric signature.
    /// </summary>
    public Result<X509Certificate> ExtractSigningCertificate(byte[] signatureData)
    {
        try
        {
            // The asymmetric signature is a CMS SignedData structure
            var cmsSignedData = new CmsSignedData(signatureData);
            
            // Get certificates from the CMS structure
            var certificates = cmsSignedData.GetCertificates();
            var certList = new List<X509Certificate>(certificates.EnumerateMatches(null));
            
            if (certList.Count == 0)
            {
                return Result.Failure<X509Certificate>("No certificates found in CHUID signature");
            }

            if (certList.Count == 1)
            {
                return Result.Success(certList[0]);
            }

            // If multiple certificates, find the signing certificate
            // Per FIPS 201-3: "If the signature on the biometric was generated with a different key 
            // than the signature on the CHUID, the certificates field shall include only a single certificate"
            var signers = cmsSignedData.GetSignerInfos();
            var signerList = signers.Cast<SignerInformation>().ToList();
            
            if (signerList.Count != 1)
            {
                return Result.Failure<X509Certificate>($"Expected exactly one signer, found {signerList.Count}");
            }

            var signerInfo = signerList[0];
            var signerId = signerInfo.SignerID;

            // Find the certificate that matches the signer
            foreach (var cert in certList)
            {
                if (signerId.Match(cert))
                {
                    return Result.Success(cert);
                }
            }

            return Result.Failure<X509Certificate>("Could not find signing certificate matching signer ID");
        }
        catch (Exception ex)
        {
            return Result.Failure<X509Certificate>($"Error extracting certificate: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a CHUID date from its byte representation.
    /// CHUID dates are in YYYYMMDD format (8 bytes ASCII).
    /// </summary>
    private DateTime? ParseChuidDate(byte[] dateBytes)
    {
        try
        {
            // CHUID dates are in YYYYMMDD format (8 bytes)
            if (dateBytes.Length != 8) return null;

            var dateString = System.Text.Encoding.ASCII.GetString(dateBytes);
            if (DateTime.TryParseExact(dateString, "yyyyMMdd", null, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }
        catch
        {
            // Ignore parse errors
        }
        return null;
    }
}