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
/// PIV facial image CBEFF format implementation following SP 800-76-2.
/// Creates CBEFF objects with INCITS 385 facial image data.
/// </summary>
public class PivFacialImageCbeffFormat : ICbeffFormat
{
    /// <summary>
    /// Gets the PIV facial image format.
    /// </summary>
    public BiometricFormat Format => BiometricFormat.WellKnown.Incits385FaceImage;

    /// <summary>
    /// Gets the supported biometric modalities (face only).
    /// </summary>
    public IReadOnlyList<BiometricModality> SupportedModalities => 
        new[] { BiometricModality.Face };

    /// <summary>
    /// Decodes CBEFF-formatted facial image data.
    /// </summary>
    public Result<CbeffPayload> Decode(byte[] cbeffData)
    {
        if (cbeffData == null)
            return Result.Failure<CbeffPayload>("CBEFF data cannot be null");

        if (cbeffData.Length < CbeffHeader.StandardHeaderSize)
            return Result.Failure<CbeffPayload>("CBEFF data too short for header");

        try
        {
            // Extract header
            var headerBytes = new byte[CbeffHeader.StandardHeaderSize];
            Array.Copy(cbeffData, 0, headerBytes, 0, CbeffHeader.StandardHeaderSize);
            
            var headerResult = CbeffHeaderSerializer.FromBytes(headerBytes);
            if (headerResult.IsFailure)
                return Result.Failure<CbeffPayload>($"Invalid CBEFF header: {headerResult.Error}");
            
            var header = headerResult.Value;
            
            // Validate this is a facial image format
            if (header.BdbFormatOwner != 0x001B || header.BdbFormatType != 0x0501)
                return Result.Failure<CbeffPayload>("Not a PIV facial image CBEFF format");

            var offset = CbeffHeader.StandardHeaderSize;
            
            // Extract biometric data block (INCITS 385 facial image)
            var biometricDataBlock = new byte[header.BdbLength];
            Array.Copy(cbeffData, offset, biometricDataBlock, 0, (int)header.BdbLength);
            offset += (int)header.BdbLength;

            // Extract signature if present
            Maybe<CbeffSignature> signature = Maybe<CbeffSignature>.None;
            if (header.SbLength > 0)
            {
                var signatureBytes = new byte[header.SbLength];
                Array.Copy(cbeffData, offset, signatureBytes, 0, header.SbLength);
                
                var signatureResult = CbeffSignature.Create(signatureBytes);
                if (signatureResult.IsFailure)
                    return Result.Failure<CbeffPayload>($"Invalid signature: {signatureResult.Error}");
                    
                signature = Maybe<CbeffSignature>.From(signatureResult.Value);
            }

            return CbeffPayload.Create(header, biometricDataBlock, signature);
        }
        catch (Exception ex)
        {
            return Result.Failure<CbeffPayload>($"Error decoding PIV facial image CBEFF: {ex.Message}");
        }
    }

    /// <summary>
    /// Encodes facial image data into PIV CBEFF format.
    /// </summary>
    public Result<byte[]> Encode(
        BiometricData biometricData,
        Maybe<CbeffSignature> signature = default)
    {
        if (biometricData.Modality != BiometricModality.Face)
            return Result.Failure<byte[]>("Biometric data must be facial image");

        if (biometricData.Data == null || biometricData.Data.Length == 0)
            return Result.Failure<byte[]>("Facial image data cannot be empty");

        try
        {
            // Extract image dimensions from JPEG 2000 data (simplified - assumes 420x560)
            var imageWidth = 420;
            var imageHeight = 560;
            
            // Create INCITS 385 biometric data block
            var incits385DataResult = CreateIncits385FacialImageBlock(
                biometricData.Data, 
                imageWidth, 
                imageHeight);
                
            if (incits385DataResult.IsFailure)
                return Result.Failure<byte[]>(incits385DataResult.Error);
            
            var biometricDataBlock = incits385DataResult.Value;
            
            // Create CBEFF header
            var signatureSize = signature.HasValue ? signature.Value.SignatureSize : (ushort)0;
            var headerResult = CreatePivFacialImageHeader(
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
            return Result.Failure<byte[]>($"Error encoding PIV facial image CBEFF: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that CBEFF data conforms to PIV facial image format.
    /// </summary>
    public Result Validate(byte[] cbeffData)
    {
        var decodeResult = Decode(cbeffData);
        return decodeResult.IsSuccess 
            ? Result.Success() 
            : Result.Failure(decodeResult.Error);
    }

    /// <summary>
    /// Creates an INCITS 385 facial image biometric data block.
    /// Based on the logic from FacialImage.cs in OpenPhysical.Net.
    /// </summary>
    private Result<byte[]> CreateIncits385FacialImageBlock(
        byte[] imageData, 
        int imageWidth, 
        int imageHeight)
    {
        try
        {
            // Create the components in the correct order
            var imageInfo = WriteImageInformation(imageWidth, imageHeight);
            var facialInfo = WriteFacialInformation(imageInfo, imageData);
            var facialHeader = WriteFacialHeader(facialInfo, imageInfo, imageData);

            // Combine: facial header + facial information + image info + image data
            var totalSize = facialHeader.Length + facialInfo.Length + imageInfo.Length + imageData.Length;
            var result = new byte[totalSize];
            var offset = 0;

            Array.Copy(facialHeader, 0, result, offset, facialHeader.Length);
            offset += facialHeader.Length;
            Array.Copy(facialInfo, 0, result, offset, facialInfo.Length);
            offset += facialInfo.Length;
            Array.Copy(imageInfo, 0, result, offset, imageInfo.Length);
            offset += imageInfo.Length;
            Array.Copy(imageData, 0, result, offset, imageData.Length);

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>($"Error creating INCITS 385 block: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates the INCITS 385 facial header.
    /// </summary>
    private byte[] WriteFacialHeader(byte[] facialInformation, byte[] imageInformation, byte[] imageData)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Constants from the original code
        var facialImageFormatIdentifier = new byte[] { (byte)'F', (byte)'A', (byte)'C', 0x00 };
        var facialImageVersionIdentifier = new byte[] { (byte)'0', (byte)'1', (byte)'0', 0x00 };
        
        const int facialHeaderLength = 0x0E;
        const ushort numberFacialRecords = 0x01;

        // Calculate total record length
        uint recordLength = (uint)(facialHeaderLength + facialInformation.Length + 
                                  imageInformation.Length + imageData.Length);
        
        var recordLengthBytes = BitConverter.GetBytes(recordLength);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(recordLengthBytes);
        }

        writer.Write(facialImageFormatIdentifier);
        writer.Write(facialImageVersionIdentifier);
        writer.Write(recordLengthBytes);

        var facialRecordBytes = BitConverter.GetBytes(numberFacialRecords);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(facialRecordBytes);
        }

        writer.Write(facialRecordBytes);

        return stream.ToArray();
    }

    /// <summary>
    /// Creates the INCITS 385 facial information block.
    /// </summary>
    private byte[] WriteFacialInformation(byte[] imageInfo, byte[] facialImageBytes)
    {
        const int facialInformationLength = 0x14;
        int facialBlockLength = facialInformationLength + imageInfo.Length + facialImageBytes.Length;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Encode the facial block length as 4 bytes (big-endian)
        var encodedFacialBlockLength = BitConverter.GetBytes(facialBlockLength);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(encodedFacialBlockLength);
        }

        writer.Write(encodedFacialBlockLength);

        // Write facial information fields
        const ushort numFeaturePoints = 0;
        var featurePointBytes = BitConverter.GetBytes(numFeaturePoints);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(featurePointBytes);
        }

        writer.Write(featurePointBytes);
        writer.Write((byte)0x00); // Gender unspecified
        writer.Write((byte)0x00); // Eye color unspecified
        writer.Write((byte)0x00); // Hair color unspecified

        // Feature mask, expression, pose angles (all unspecified/neutral)
        writer.Write(new byte[] { 0x00, 0x00, 0x00 }); // Feature mask
        writer.Write(new byte[] { 0x00, 0x01 });       // Expression: neutral, eyes open, mouth closed
        writer.Write(new byte[] { 0x00, 0x00, 0x00 }); // Pose angles
        writer.Write(new byte[] { 0x00, 0x00, 0x00 }); // Pose angle uncertainty

        return stream.ToArray();
    }

    /// <summary>
    /// Creates the INCITS 385 image information block.
    /// </summary>
    private byte[] WriteImageInformation(int facialImageWidth, int facialImageHeight)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Image type and format
        writer.Write((byte)0x01); // Full frontal
        writer.Write((byte)0x01); // JPEG 2000

        // Width and height (big-endian)
        var widthBytes = BitConverter.GetBytes((ushort)facialImageWidth);
        var heightBytes = BitConverter.GetBytes((ushort)facialImageHeight);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(widthBytes);
            Array.Reverse(heightBytes);
        }

        writer.Write(widthBytes);
        writer.Write(heightBytes);

        writer.Write((byte)0x01); // RGB color space
        writer.Write((byte)0x00); // Source type unspecified

        // Device type (big-endian, unspecified)
        writer.Write(new byte[] { 0x00, 0x00 });
        writer.Write((ushort)0x00); // Quality unspecified

        return stream.ToArray();
    }

    /// <summary>
    /// Creates a PIV-compliant CBEFF header for facial images.
    /// </summary>
    private Result<CbeffHeader> CreatePivFacialImageHeader(
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

            // BDB format owner and type for facial images
            headerBytes[offset++] = 0x00; headerBytes[offset++] = 0x1B; // Owner: INCITS TC M1
            headerBytes[offset++] = 0x05; headerBytes[offset++] = 0x01; // Type: Facial image

            // Dates (8 bytes each, BCD format)
            var dateBytes = DateTimeCodec.Encode(captureDate);
            Array.Copy(dateBytes, 0, headerBytes, offset, 8); offset += 8; // Creation
            Array.Copy(dateBytes, 0, headerBytes, offset, 8); offset += 8; // Validity start
            
            var expirationBytes = DateTimeCodec.Encode(captureDate.AddYears(3));
            Array.Copy(expirationBytes, 0, headerBytes, offset, 8); offset += 8; // Validity end

            // Biometric type (4 bytes: 0x00, 0x00, 0x00, 0x02 for face)
            headerBytes[offset++] = 0x00;
            headerBytes[offset++] = 0x00;
            headerBytes[offset++] = 0x00;
            headerBytes[offset++] = 0x02;

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
            return Result.Failure<CbeffHeader>($"Error creating PIV facial image header: {ex.Message}");
        }
    }
}