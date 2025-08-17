namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Cbeff;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// PIV fingerprint template CBEFF format implementation following SP 800-76-2.
/// Creates CBEFF objects with INCITS 378 fingerprint minutiae data.
/// Supports empty templates for individuals who cannot be fingerprinted per Section 4.4.3.
/// </summary>
public class PivFingerprintCbeffFormat : ICbeffFormat
{
    /// <summary>
    /// Gets the PIV fingerprint format.
    /// </summary>
    public BiometricFormat Format => BiometricFormat.WellKnown.Incits378Minutiae;

    /// <summary>
    /// Gets the supported biometric modalities (fingerprint only).
    /// </summary>
    public IReadOnlyList<BiometricModality> SupportedModalities => 
        new[] { BiometricModality.Fingerprint };

    /// <summary>
    /// Decodes CBEFF-formatted fingerprint template data.
    /// </summary>
    public Result<CbeffPayload> Decode(byte[] cbeffData)
    {
        if (cbeffData == null)
            return Result.Failure<CbeffPayload>("CBEFF data cannot be null");

        if (cbeffData.Length < CbeffHeader.StandardHeaderSize)
            return Result.Failure<CbeffPayload>("CBEFF data too short for header");

        try
        {
            byte[] cbeffPayload = cbeffData;
            
            // Check for BC tag wrapper (tag 0xBC for biometric templates in PIV cards)
            // BC tag is Context-specific, Constructed, tag 28 (0xBC)
            // However, the contents are NOT ASN.1 - they are raw CBEFF bytes
            // So we need to parse the tag/length manually to avoid ASN.1 parsing errors
            if (cbeffData.Length > 3 && cbeffData[0] == 0xBC)
            {
                // Parse BER/DER length manually
                int berOffset = 1;
                int payloadLength;
                
                if (cbeffData[berOffset] == 0x82)
                {
                    // Long form: 2 bytes
                    payloadLength = (cbeffData[berOffset + 1] << 8) | cbeffData[berOffset + 2];
                    berOffset += 3;
                }
                else if (cbeffData[berOffset] == 0x81)
                {
                    // Long form: 1 byte
                    payloadLength = cbeffData[berOffset + 1];
                    berOffset += 2;
                }
                else if (cbeffData[berOffset] < 0x80)
                {
                    // Short form
                    payloadLength = cbeffData[berOffset];
                    berOffset += 1;
                }
                else
                {
                    return Result.Failure<CbeffPayload>("Unsupported BER length encoding in BC tag");
                }
                
                // Validate we have enough data
                if (berOffset + payloadLength > cbeffData.Length)
                {
                    return Result.Failure<CbeffPayload>($"Invalid BC length: claims {payloadLength} bytes but only {cbeffData.Length - berOffset} available");
                }
                
                // Extract the raw CBEFF payload (without ASN.1 parsing)
                cbeffPayload = new byte[payloadLength];
                Array.Copy(cbeffData, berOffset, cbeffPayload, 0, payloadLength);
            }
            
            // Extract header from the CBEFF payload
            var headerBytes = new byte[CbeffHeader.StandardHeaderSize];
            Array.Copy(cbeffPayload, 0, headerBytes, 0, CbeffHeader.StandardHeaderSize);
            
            // Debug: Log the first few bytes of the CBEFF header
            System.Console.WriteLine($"CBEFF payload first 32 bytes: {BitConverter.ToString(cbeffPayload, 0, Math.Min(32, cbeffPayload.Length))}");
            
            var headerResult = CbeffHeaderSerializer.FromBytes(headerBytes);
            if (headerResult.IsFailure)
                return Result.Failure<CbeffPayload>($"Invalid CBEFF header: {headerResult.Error}");
            
            var header = headerResult.Value;
            
            // Validate this is a fingerprint template format
            if (header.BdbFormatOwner != 0x001B || header.BdbFormatType != 0x0201)
                return Result.Failure<CbeffPayload>("Not a PIV fingerprint template CBEFF format");

            var offset = CbeffHeader.StandardHeaderSize;
            
            // Extract biometric data block (INCITS 378 template)
            var biometricDataBlock = new byte[header.BdbLength];
            Array.Copy(cbeffPayload, offset, biometricDataBlock, 0, (int)header.BdbLength);
            offset += (int)header.BdbLength;

            // Extract signature if present
            Maybe<CbeffSignature> signature = Maybe<CbeffSignature>.None;
            if (header.SbLength > 0)
            {
                var signatureBytes = new byte[header.SbLength];
                Array.Copy(cbeffPayload, offset, signatureBytes, 0, header.SbLength);
                
                var signatureResult = CbeffSignature.Create(signatureBytes);
                if (signatureResult.IsFailure)
                    return Result.Failure<CbeffPayload>($"Invalid signature: {signatureResult.Error}");
                    
                signature = Maybe<CbeffSignature>.From(signatureResult.Value);
            }

            return CbeffPayload.Create(header, biometricDataBlock, signature);
        }
        catch (Exception ex)
        {
            return Result.Failure<CbeffPayload>($"Error decoding PIV fingerprint CBEFF: {ex.Message}");
        }
    }

    /// <summary>
    /// Encodes fingerprint template data into PIV CBEFF format.
    /// </summary>
    public Result<byte[]> Encode(
        BiometricData biometricData,
        Maybe<CbeffSignature> signature = default)
    {
        if (biometricData.Modality != BiometricModality.Fingerprint)
            return Result.Failure<byte[]>("Biometric data must be fingerprint template");

        try
        {
            // Create INCITS 378 biometric data block
            var incits378DataResult = CreateIncits378Template(
                biometricData.Quality == -2 || biometricData.Quality == -1);
                
            if (incits378DataResult.IsFailure)
                return Result.Failure<byte[]>(incits378DataResult.Error);
            
            var biometricDataBlock = incits378DataResult.Value;
            
            // Create CBEFF header
            var signatureSize = signature.HasValue ? signature.Value.SignatureSize : (ushort)0;
            var headerResult = CreatePivFingerprintHeader(
                (uint)biometricDataBlock.Length,
                signatureSize,
                biometricData.CaptureDate,
                biometricData.Quality,
                biometricData.FascN ?? new byte[25]);
                
            if (headerResult.IsFailure)
                return Result.Failure<byte[]>(headerResult.Error);
            
            var header = headerResult.Value;
            
            // Combine header + BDB + signature
            var totalSize = CbeffHeader.StandardHeaderSize + biometricDataBlock.Length + signatureSize;
            var result = new byte[totalSize];
            var offset = 0;
            
            // Copy header
            var headerBytes = CbeffHeaderSerializer.ToBytes(header);
            Array.Copy(headerBytes, 0, result, offset, headerBytes.Length);
            offset += headerBytes.Length;
            
            // Copy biometric data block
            Array.Copy(biometricDataBlock, 0, result, offset, biometricDataBlock.Length);
            offset += biometricDataBlock.Length;
            
            // Copy signature if present
            if (signature.HasValue)
            {
                Array.Copy(signature.Value.SignatureData, 0, result, offset, signature.Value.SignatureData.Length);
            }
            
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>($"Error encoding PIV fingerprint CBEFF: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that CBEFF data conforms to PIV fingerprint format.
    /// </summary>
    public Result Validate(byte[] cbeffData)
    {
        var decodeResult = Decode(cbeffData);
        return decodeResult.IsSuccess 
            ? Result.Success() 
            : Result.Failure(decodeResult.Error);
    }

    /// <summary>
    /// Creates an empty INCITS 378 fingerprint template per SP 800-76-2 Section 4.4.3.
    /// Contains two empty views (zero minutiae) for individuals who cannot be fingerprinted.
    /// </summary>
    private Result<byte[]> CreateIncits378Template(bool isUnusable = true)
    {
        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // Constants from the original code
            var formatIdentifier = new byte[] { 0x46, 0x4D, 0x52, 0x00 }; // "FMR\0"
            var formatVersion = new byte[] { 0x20, 0x32, 0x30, 0x00 };    // " 20\0"
            
            const ushort nistCbeffProductOwner = 0x0001;
            const ushort nistCbeffProductType = 0x0001;
            const byte compliantEftsAppendixF = 0x80;
            const byte captureEquipmentId = 0x01;
            const ushort horizontalResolutionPpc = (ushort)(600 / 2.54); // 600 DPI to pixels per cm
            const ushort verticalResolutionPpc = (ushort)(600 / 2.54);

            // General Record Header per INCITS 378-2004 Table 6
            writer.Write(formatIdentifier);     // "FMR\0"
            writer.Write(formatVersion);        // " 20\0"

            // Calculate record length: header (26 bytes) + 2 finger view records (6 bytes each) = 38 bytes
            const ushort recordLength = 38;
            var recordLengthBytes = BitConverter.GetBytes(recordLength);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(recordLengthBytes);
            }
            writer.Write(recordLengthBytes);

            // CBEFF Product Owner (2 bytes, big-endian)
            var productOwnerBytes = BitConverter.GetBytes(nistCbeffProductOwner);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(productOwnerBytes);
            }
            writer.Write(productOwnerBytes);

            // CBEFF Product Type (2 bytes, big-endian)
            var productTypeBytes = BitConverter.GetBytes(nistCbeffProductType);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(productTypeBytes);
            }
            writer.Write(productTypeBytes);

            // Capture equipment compliance
            writer.Write(compliantEftsAppendixF);
            writer.Write(captureEquipmentId);

            // Resolution (2 bytes each, big-endian, pixels per cm)
            var horizontalResBytes = BitConverter.GetBytes(horizontalResolutionPpc);
            var verticalResBytes = BitConverter.GetBytes(verticalResolutionPpc);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(horizontalResBytes);
                Array.Reverse(verticalResBytes);
            }
            writer.Write(horizontalResBytes);
            writer.Write(verticalResBytes);

            // Number of finger records (2 views for primary and secondary finger)
            const byte numberOfFingerRecords = 2;
            writer.Write(numberOfFingerRecords);

            // Reserved byte
            writer.Write((byte)0x00);

            // Finger View Record 1 (Primary finger) - Empty view
            WriteEmptyFingerViewRecord(writer, fingerPosition: 7, viewNumber: 0, isUnusable); // Right index

            // Finger View Record 2 (Secondary finger) - Empty view  
            WriteEmptyFingerViewRecord(writer, fingerPosition: 2, viewNumber: 0, isUnusable); // Left index

            return Result.Success(stream.ToArray());
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>($"Error creating INCITS 378 template: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes an empty finger view record per SP 800-76-2 Section 4.4.3.
    /// </summary>
    private void WriteEmptyFingerViewRecord(BinaryWriter writer, byte fingerPosition, byte viewNumber, bool isUnusable)
    {
        // Constants for unusable fingerprints
        const byte biometricQualityPermanentlyUnusable = 254;
        const byte biometricQualityTemporarilyUnusable = 255;

        // Finger position and view number
        writer.Write(fingerPosition);
        writer.Write(viewNumber);

        // Impression type (0 = Live-scan plain)
        writer.Write((byte)0x00);

        // Finger quality - set based on unusable status
        byte fingerQuality = isUnusable ? biometricQualityTemporarilyUnusable : biometricQualityPermanentlyUnusable;
        writer.Write(fingerQuality);

        // Number of minutiae (0 for empty template per Section 4.4.3)
        writer.Write((byte)0x00);

        // Reserved byte
        writer.Write((byte)0x00);
    }

    /// <summary>
    /// Creates a PIV-compliant CBEFF header for fingerprint templates.
    /// </summary>
    private Result<CbeffHeader> CreatePivFingerprintHeader(
        uint bdbLength,
        ushort sbLength,
        DateTime captureDate,
        sbyte quality,
        byte[] fascN)
    {
        try
        {
            // Create the complete header in byte form, then parse it
            var headerBytes = new byte[CbeffHeader.StandardHeaderSize];
            var offset = 0;

            // Basic header fields
            headerBytes[offset++] = 0x03; // Patron header version
            headerBytes[offset++] = 0x0D; // Security options (signed plaintext)

            // BDB length (4 bytes, big-endian)
            headerBytes[offset++] = (byte)(bdbLength >> 24);
            headerBytes[offset++] = (byte)(bdbLength >> 16);
            headerBytes[offset++] = (byte)(bdbLength >> 8);
            headerBytes[offset++] = (byte)bdbLength;

            // SB length (2 bytes, big-endian)
            headerBytes[offset++] = (byte)(sbLength >> 8);
            headerBytes[offset++] = (byte)sbLength;

            // BDB format owner and type for fingerprint minutiae
            headerBytes[offset++] = 0x00; headerBytes[offset++] = 0x1B; // Owner: INCITS TC M1
            headerBytes[offset++] = 0x02; headerBytes[offset++] = 0x01; // Type: Minutiae template

            // Dates (8 bytes each, NIST PIV format)
            var dateBytes = DateTimeCodec.Encode(captureDate);
            Array.Copy(dateBytes, 0, headerBytes, offset, 8); offset += 8; // Creation
            Array.Copy(dateBytes, 0, headerBytes, offset, 8); offset += 8; // Validity start
            
            var expirationBytes = DateTimeCodec.Encode(captureDate.AddYears(3));
            Array.Copy(expirationBytes, 0, headerBytes, offset, 8); offset += 8; // Validity end

            // Biometric type (3 bytes per NIST SP 800-76-2 Table 14: 0x00, 0x00, 0x08 for fingerprint)
            headerBytes[offset++] = 0x00;
            headerBytes[offset++] = 0x00;
            headerBytes[offset++] = 0x08;

            // Biometric subtype and purpose
            headerBytes[offset++] = 0x00; // Subtype
            headerBytes[offset++] = 0x00; // Purpose

            // Quality
            headerBytes[offset++] = (byte)quality;

            // Creator (18 bytes, "OpenPhysical" padded with zeros)
            var creatorBytes = new byte[18];
            var creatorName = Encoding.ASCII.GetBytes("OpenPhysical");
            Array.Copy(creatorName, 0, creatorBytes, 0, Math.Min(creatorName.Length, 18));
            Array.Copy(creatorBytes, 0, headerBytes, offset, 18);
            offset += 18;

            // FASC-N (25 bytes)
            Array.Copy(fascN, 0, headerBytes, offset, 25);
            offset += 25;

            // Reserved (4 bytes, all zero)
            Array.Clear(headerBytes, offset, 4);

            return CbeffHeaderSerializer.FromBytes(headerBytes);
        }
        catch (Exception ex)
        {
            return Result.Failure<CbeffHeader>($"Error creating PIV fingerprint header: {ex.Message}");
        }
    }
}