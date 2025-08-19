namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Domains.Cbeff;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Attribute = Org.BouncyCastle.Asn1.Cms.Attribute;

/// <summary>
/// Service for signing CBEFF payloads using CMS (Cryptographic Message Syntax).
/// Based on the signing logic from SignedDataObject.cs in OpenPhysical.Net.
/// </summary>
public class CbeffSignerService : ICbeffSigner
{
    private static readonly DerObjectIdentifier CardUuidOid = new DerObjectIdentifier("1.3.6.1.4.1.57923.1.1.20.2");
    
    /// <summary>
    /// Signs a CBEFF payload using CMS.
    /// </summary>
    public async Task<Result<CbeffPayload>> SignAsync(
        CbeffPayload payload,
        X509Certificate signingCertificate,
        AsymmetricKeyParameter privateKey,
        Maybe<Guid> includeCardUuid = default)
    {
        await Task.CompletedTask;
        
        // Create CBEFF header + BDB bytes for signing
        var cbeffData = SerializeCbeffForSigning(payload);
        
        // Calculate the target signature size
        var targetSize = CalculateTargetSignatureSize(
            cbeffData, 
            signingCertificate, 
            privateKey, 
            includeCardUuid);
        
        // Create a new header with the signature size
        var headerResult = CbeffHeader.Create(
            payload.Header.PatronHeaderVersion,
            payload.Header.SbhSecurityOptions,
            payload.Header.BdbLength,
            targetSize,
            payload.Header.BdbFormatOwner,
            payload.Header.BdbFormatType,
            payload.Header.CreationDate,
            payload.Header.ValidityNotBefore,
            payload.Header.ValidityNotAfter,
            payload.Header.BiometricType,
            payload.Header.BiometricSubtype,
            payload.Header.Quality,
            payload.Header.GetCreator(),
            payload.Header.GetFascN(),
            payload.Header.GetReserved());
            
        if (headerResult.IsFailure)
            return Result.Failure<CbeffPayload>(headerResult.Error);
            
        var updatedHeader = headerResult.Value;
        var payloadWithSize = new CbeffPayload(
            updatedHeader,
            payload.BiometricDataBlock,
            payload.Signature);
        
        // Sign the updated payload
        var updatedCbeffData = SerializeCbeffForSigning(payloadWithSize);
        var signatureResult = CreateDetachedSignatureInternal(
            updatedCbeffData,
            signingCertificate,
            privateKey,
            includeCardUuid,
            targetSize);
        
        return signatureResult
            .Bind(signatureBytes => CbeffSignature.Create(signatureBytes))
            .Bind(signature => CreatePayloadWithCorrectHeader(payloadWithSize, signature));
    }

    /// <summary>
    /// Creates a detached CMS signature for CBEFF data.
    /// </summary>
    public Result<byte[]> CreateDetachedSignature(
        byte[] dataToSign,
        X509Certificate signingCertificate,
        AsymmetricKeyParameter privateKey,
        Maybe<Guid> includeCardUuid = default)
    {
        return Result.Try(() =>
        {
            var cmsGenerator = CreateCmsSignedDataGenerator(signingCertificate, privateKey, includeCardUuid);
            var content = new CmsProcessableByteArray(dataToSign);
            
            // Create detached signature
            var signedData = cmsGenerator.Generate(
                CmsSignedGenerator.Data, 
                content, 
                false); // encapsulate = false for detached
            
            return signedData.GetEncoded("DER");
        }, ex => $"Signature creation failed: {ex.Message}");
    }

    /// <summary>
    /// Calculates the signature size for a given certificate and private key.
    /// This is needed for pre-allocating space in the CBEFF header.
    /// </summary>
    public ushort EstimateSignatureSize(
        X509Certificate signingCertificate,
        AsymmetricKeyParameter privateKey)
    {
        return Result.Try(() =>
        {
            // Create dummy data to sign
            var dummyData = new byte[100];
            var cmsGenerator = CreateCmsSignedDataGenerator(signingCertificate, privateKey, Maybe<Guid>.None);
            var content = new CmsProcessableByteArray(dummyData);
            
            // Generate several signatures to find the most common size
            var sizes = new List<int>();
            for (int i = 0; i < 9; i++)
            {
                var signedData = cmsGenerator.Generate(CmsSignedGenerator.Data, content, false);
                sizes.Add(signedData.GetEncoded("DER").Length);
            }
            
            // Return the most common size
            var mostCommonSize = sizes
                .GroupBy(s => s)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .First();
            
            return (ushort)mostCommonSize;
        })
        .GetValueOrDefault((ushort)1024); // Default fallback on error
    }
    
    private CmsSignedDataGenerator CreateCmsSignedDataGenerator(
        X509Certificate certificate,
        AsymmetricKeyParameter privateKey,
        Maybe<Guid> includeCardUuid)
    {
        var cmsGenerator = new CmsSignedDataGenerator();
        
        // Determine signature algorithm based on key type
        string signatureAlgorithm;
        if (privateKey is Org.BouncyCastle.Crypto.Parameters.ECPrivateKeyParameters)
        {
            signatureAlgorithm = "SHA256withECDSA";
        }
        else if (privateKey is Org.BouncyCastle.Crypto.Parameters.RsaPrivateCrtKeyParameters)
        {
            signatureAlgorithm = "SHA256withRSA";
        }
        else
        {
            signatureAlgorithm = "SHA256withRSA"; // Default
        }
        
        var contentSigner = new Asn1SignatureFactory(signatureAlgorithm, privateKey);
        
        if (includeCardUuid.HasValue)
        {
            // Include card UUID as a signed attribute
            var cardUuidBytes = includeCardUuid.Value.ToByteArray();
            var customAttribute = new DerTaggedObject(false, 0, new DerOctetString(cardUuidBytes));
            var attributes = new Asn1EncodableVector();
            attributes.Add(new Attribute(CardUuidOid, new DerSet(customAttribute)));
            var attributeTable = new AttributeTable(new DerSet(attributes));
            
            var signerInfoGenerator = new SignerInfoGeneratorBuilder()
                .WithSignedAttributeGenerator(new DefaultSignedAttributeTableGenerator(attributeTable))
                .Build(contentSigner, certificate);
            
            cmsGenerator.AddSignerInfoGenerator(signerInfoGenerator);
        }
        else
        {
            // Standard signing without custom attributes
            cmsGenerator.AddSigner(privateKey, certificate, CmsSignedGenerator.DigestSha256);
        }
        
        return cmsGenerator;
    }
    
    private ushort CalculateTargetSignatureSize(
        byte[] dataToSign,
        X509Certificate certificate,
        AsymmetricKeyParameter privateKey,
        Maybe<Guid> includeCardUuid)
    {
        var cmsGenerator = CreateCmsSignedDataGenerator(certificate, privateKey, includeCardUuid);
        var content = new CmsProcessableByteArray(dataToSign);
        var sizes = new List<int>();
        
        // Generate multiple signatures to find the most common size
        for (int i = 0; i < 9; i++)
        {
            var signedData = cmsGenerator.Generate(CmsSignedGenerator.Data, content, false);
            sizes.Add(signedData.GetEncoded("DER").Length);
        }
        
        // Return the most common size
        return (ushort)sizes
            .GroupBy(s => s)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .First();
    }
    
    private Result<byte[]> CreateDetachedSignatureInternal(
        byte[] dataToSign,
        X509Certificate certificate,
        AsymmetricKeyParameter privateKey,
        Maybe<Guid> includeCardUuid,
        ushort targetSize)
    {
        var cmsGenerator = CreateCmsSignedDataGenerator(certificate, privateKey, includeCardUuid);
        var content = new CmsProcessableByteArray(dataToSign);
        
        // Try up to 32 times to get the target signature size
        for (int i = 0; i < 32; i++)
        {
            var signedData = cmsGenerator.Generate(CmsSignedGenerator.Data, content, false);
            var signatureBytes = signedData.GetEncoded("DER");
            
            if (signatureBytes.Length == targetSize)
            {
                return Result.Success(signatureBytes);
            }
        }
        
        // If we couldn't get the exact size, return the last attempt
        var finalSignedData = cmsGenerator.Generate(CmsSignedGenerator.Data, content, false);
        return Result.Success(finalSignedData.GetEncoded("DER"));
    }
    
    private byte[] SerializeCbeffForSigning(CbeffPayload payload)
    {
        // Serialize CBEFF header + BDB for signing according to SP 800-76-2
        // The signature covers the header (88 bytes) + the biometric data block
        var headerBytes = SerializeCbeffHeader(payload.Header);
        var bdbBytes = payload.GetBiometricDataBlock();
        
        var result = new byte[headerBytes.Length + bdbBytes.Length];
        Array.Copy(headerBytes, 0, result, 0, headerBytes.Length);
        Array.Copy(bdbBytes, 0, result, headerBytes.Length, bdbBytes.Length);
        
        return result;
    }
    
    private byte[] SerializeCbeffHeader(CbeffHeader header)
    {
        // CBEFF header is exactly 88 bytes per SP 800-76-2
        var buffer = new byte[CbeffHeader.StandardHeaderSize];
        var offset = 0;
        
        // Patron Header Version (1 byte)
        buffer[offset++] = header.PatronHeaderVersion;
        
        // SBH Security Options (1 byte)
        buffer[offset++] = header.SbhSecurityOptions;
        
        // BDB Length (4 bytes, big-endian)
        WriteBigEndianUInt32(buffer, offset, header.BdbLength);
        offset += 4;
        
        // SB Length (2 bytes, big-endian)
        WriteBigEndianUInt16(buffer, offset, header.SbLength);
        offset += 2;
        
        // BDB Format Owner (2 bytes, big-endian)
        WriteBigEndianUInt16(buffer, offset, header.BdbFormatOwner);
        offset += 2;
        
        // BDB Format Type (2 bytes, big-endian)
        WriteBigEndianUInt16(buffer, offset, header.BdbFormatType);
        offset += 2;
        
        // Creation Date (8 bytes)
        WriteDateTime(buffer, offset, header.CreationDate);
        offset += 8;
        
        // Validity Period Not Before (8 bytes)
        WriteDateTime(buffer, offset, header.ValidityNotBefore);
        offset += 8;
        
        // Validity Period Not After (8 bytes)
        WriteDateTime(buffer, offset, header.ValidityNotAfter);
        offset += 8;
        
        // Biometric Type (3 bytes, big-endian)
        WriteBigEndianUInt24(buffer, offset, header.BiometricType);
        offset += 3;
        
        // Biometric Subtype (1 byte)
        buffer[offset++] = header.BiometricSubtype;
        
        // Biometric Quality (1 byte)
        buffer[offset++] = (byte)header.Quality;
        
        // Creator (18 bytes)
        Array.Copy(header.GetCreator(), 0, buffer, offset, 18);
        offset += 18;
        
        // FASC-N (25 bytes)
        Array.Copy(header.GetFascN(), 0, buffer, offset, 25);
        offset += 25;
        
        // Reserved (4 bytes)
        Array.Copy(header.GetReserved(), 0, buffer, offset, 4);
        
        return buffer;
    }
    
    private static void WriteBigEndianUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }
    
    private static void WriteBigEndianUInt24(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 16);
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)value;
    }
    
    private static void WriteBigEndianUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
    
    /// <summary>
    /// Writes a DateTime to the buffer in CBEFF format per NIST SP 800-76-2, Section 6.2.
    /// 
    /// Format: "YYYYMMDDhhmmssZ" encoded as 8 bytes where each pair of characters
    /// is coded as an unsigned integer (NOT BCD). The 'Z' character (0x5A) indicates UTC.
    /// 
    /// Example: December 15, 2005 at 17:35:30 UTC is represented as:
    /// [0x14, 0x05, 0x0C, 0x0F, 0x11, 0x23, 0x1E, 0x5A]
    /// Which corresponds to: [20, 05, 12, 15, 17, 35, 30, 'Z']
    /// 
    /// This is NOT Binary Coded Decimal (BCD) - each byte is a simple unsigned integer.
    /// </summary>
    private static void WriteDateTime(byte[] buffer, int offset, DateTime dateTime)
    {
        // Per NIST SP 800-76-2: "Each pair of characters (for example, "DD") 
        // is coded in 8 bits as an unsigned integer"
        // This means we store the numeric values directly, NOT as BCD
        
        var year = dateTime.Year;
        var month = dateTime.Month;
        var day = dateTime.Day;
        var hour = dateTime.Hour;
        var minute = dateTime.Minute;
        var second = dateTime.Second;
        
        // Store as unsigned integers (not BCD)
        buffer[offset] = (byte)(year / 100);     // Century (e.g., 20 for 2005)
        buffer[offset + 1] = (byte)(year % 100); // Year within century (e.g., 05 for 2005)
        buffer[offset + 2] = (byte)month;        // Month (1-12)
        buffer[offset + 3] = (byte)day;          // Day (1-31)
        buffer[offset + 4] = (byte)hour;         // Hour (0-23)
        buffer[offset + 5] = (byte)minute;       // Minute (0-59)
        buffer[offset + 6] = (byte)second;       // Second (0-59)
        buffer[offset + 7] = 0x5A;               // 'Z' character (0x5A) for UTC
    }
    
    /// <summary>
    /// Creates a CBEFF payload with the correct header that matches the actual signature size.
    /// </summary>
    private Result<CbeffPayload> CreatePayloadWithCorrectHeader(CbeffPayload payload, CbeffSignature signature)
    {
        // Create a new header with the actual signature size
        var headerResult = CbeffHeader.Create(
            payload.Header.PatronHeaderVersion,
            payload.Header.SbhSecurityOptions,
            payload.Header.BdbLength,
            signature.SignatureSize, // Use actual signature size
            payload.Header.BdbFormatOwner,
            payload.Header.BdbFormatType,
            payload.Header.CreationDate,
            payload.Header.ValidityNotBefore,
            payload.Header.ValidityNotAfter,
            payload.Header.BiometricType,
            payload.Header.BiometricSubtype,
            payload.Header.Quality,
            payload.Header.GetCreator(),
            payload.Header.GetFascN(),
            payload.Header.GetReserved());
            
        return headerResult
            .Map(correctedHeader => new CbeffPayload(
                correctedHeader,
                payload.BiometricDataBlock,
                Maybe<CbeffSignature>.From(signature)));
    }
}