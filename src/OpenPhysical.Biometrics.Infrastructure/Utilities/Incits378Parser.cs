namespace OpenPhysical.Biometrics.Infrastructure.Utilities;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;
using System.Collections.Immutable;

/// <summary>
/// Utility class for parsing INCITS 378 fingerprint minutiae format data.
/// </summary>
public static class Incits378Parser
{
    /// <summary>
    /// Parses INCITS 378 fingerprint minutiae data.
    /// </summary>
    /// <param name="data">The raw biometric data bytes.</param>
    /// <returns>A Result containing the parsed fingerprint record or an error.</returns>
    public static Result<FingerprintRecord> ParseIncits378Record(ImmutableArray<byte> data)
    {
        if (data.IsDefaultOrEmpty)
            return Result.Failure<FingerprintRecord>("Biometric data is empty");
            
        if (data.Length < 26) // Minimum header size for INCITS 378
            return Result.Failure<FingerprintRecord>("Biometric data too small for INCITS 378 header");
            
        var offset = 0;
        
        // Parse general record header (26 bytes per INCITS 378-2004 Table 6)
        return ParseGeneralRecordHeader(data, ref offset)
            .Bind(header => {
                // Validate that we have enough data for the complete record
                if (data.Length < header.RecordLength)
                {
                    return Result.Failure<FingerprintRecord>(
                        $"Data length ({data.Length} bytes) is less than record length ({header.RecordLength} bytes)");
                }
                
                // DEBUG: Handle case where NumberOfFingerRecords = 0 but data is present
                // This might be extended data blocks or finger view records with different interpretation
                if (header.NumberOfFingerRecords == 0 && offset < header.RecordLength)
                {
                    var remainingBytes = (int)(header.RecordLength - offset);
                    
                    // DEBUG: Try to determine if this might be finger view records anyway
                    if (remainingBytes >= 6) // Minimum for finger view record header
                    {
                        var debugFingerPosition = data[offset];
                        var debugViewNumber = data[offset + 1];
                        var debugImpressionType = data[offset + 2];
                        var debugFingerQuality = data[offset + 3];
                        var debugNumberOfMinutiae = data[offset + 4];
                        var debugReserved = data[offset + 5];
                        
                        return Result.Failure<FingerprintRecord>(
                            $"INCITS 378 DEBUG: NumberOfFingerRecords=0 but {remainingBytes} bytes remain. " +
                            $"Next bytes look like: Pos={debugFingerPosition}, View={debugViewNumber}, " +
                            $"ImpType={debugImpressionType}, Quality={debugFingerQuality}, " +
                            $"Minutiae={debugNumberOfMinutiae}, Reserved=0x{debugReserved:X2}. " +
                            $"This suggests extended data blocks or misinterpreted header.");
                    }
                    
                    return Result.Failure<FingerprintRecord>(
                        $"INCITS 378 format variant: NumberOfFingerRecords is 0 but {remainingBytes} bytes of data remain.");
                }
                
                return ParseFingerViewRecords(data, ref offset, header.NumberOfFingerRecords)
                    .Map(viewRecords => new FingerprintRecord(header, viewRecords));
            });
    }
    
    private static Result<GeneralRecordHeader> ParseGeneralRecordHeader(ImmutableArray<byte> data, ref int offset)
    {
        // Need minimum 10 bytes to determine record length format (8 bytes format/version + 2 bytes minimum length)
        if (offset + 10 > data.Length)
            return Result.Failure<GeneralRecordHeader>("Insufficient data for general record header");
            
        // Format identifier - "FMR\0"
        var formatId = ReadBytes(data, offset, 4);
        offset += 4;
        
        // Version number - " 20\0"  
        var version = ReadBytes(data, offset, 4);
        offset += 4;
        
        // Record length (2 or 6 bytes, big-endian)
        var (recordLength, bytesConsumed) = ReadRecordLength(data, offset);
        offset += bytesConsumed;
        
        // Now we know the record length format, validate we have enough data for the remaining header fields
        // Remaining fields: ProductOwner(2) + ProductType(2) + CaptureEquipment(2) + 
        // ImageSizeX(2) + ImageSizeY(2) + XResolution(2) + YResolution(2) + NumberOfFingerRecords(1) + Reserved(1) = 14 bytes
        if (offset + 14 > data.Length)
            return Result.Failure<GeneralRecordHeader>("Insufficient data for complete general record header");
        
        // CBEFF Product Owner (2 bytes, big-endian)
        var productOwner = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // CBEFF Product Type (2 bytes, big-endian)
        var productType = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // Capture equipment compliance and ID (2 bytes, big-endian)
        // Bits 15-12: Equipment compliance (4 bits), Bits 11-0: Equipment ID (12 bits)
        var captureEquipmentCompliance = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // Image size X (width) in pixels (2 bytes, big-endian)
        var imageSizeX = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // Image size Y (height) in pixels (2 bytes, big-endian)
        var imageSizeY = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // X scan resolution (2 bytes, big-endian, pixels per cm)
        var xResolution = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // Y scan resolution (2 bytes, big-endian, pixels per cm)
        var yResolution = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // Number of finger records (1 byte)
        var numberOfFingerRecords = data[offset++];
        
        // Reserved (1 byte)
        var reserved = data[offset++];
        
        return GeneralRecordHeader.Create(
            formatId.ToImmutableArray(),
            version.ToImmutableArray(),
            recordLength,
            productOwner,
            productType,
            captureEquipmentCompliance,
            imageSizeX,
            imageSizeY,
            xResolution,
            yResolution,
            numberOfFingerRecords,
            reserved);
    }
    
    private static Result<ImmutableArray<FingerViewRecord>> ParseFingerViewRecords(
        ImmutableArray<byte> data, ref int offset, byte count)
    {
        var viewRecords = new List<FingerViewRecord>();
        
        for (int i = 0; i < count; i++)
        {
            var recordResult = ParseFingerViewRecord(data, ref offset);
            if (recordResult.IsFailure)
                return Result.Failure<ImmutableArray<FingerViewRecord>>(recordResult.Error);
                
            viewRecords.Add(recordResult.Value);
        }
        
        return Result.Success(viewRecords.ToImmutableArray());
    }
    
    private static Result<FingerViewRecord> ParseFingerViewRecord(ImmutableArray<byte> data, ref int offset)
    {
        if (offset + 6 > data.Length)
            return Result.Failure<FingerViewRecord>("Insufficient data for finger view record header");
            
        // Finger position (1 byte)
        var fingerPosition = data[offset++];
        
        // View number (1 byte)
        var viewNumber = data[offset++];
        
        // Impression type (1 byte)
        var impressionType = data[offset++];
        
        // Finger quality (1 byte)
        var fingerQuality = data[offset++];
        
        // Number of minutiae (1 byte)
        var numberOfMinutiae = data[offset++];
        
        // Reserved (1 byte)
        var reserved = data[offset++];
        
        
        // Parse minutiae data if present
        var minutiae = ImmutableArray<MinutiaPoint>.Empty;
        if (numberOfMinutiae > 0)
        {
            var minutiaeResult = ParseMinutiae(data, ref offset, numberOfMinutiae);
            if (minutiaeResult.IsFailure)
                return Result.Failure<FingerViewRecord>(minutiaeResult.Error);
                
            minutiae = minutiaeResult.Value;
        }
        
        return FingerViewRecord.Create(
            fingerPosition,
            viewNumber,
            impressionType,
            fingerQuality,
            numberOfMinutiae,
            reserved,
            minutiae);
    }
    
    private static Result<ImmutableArray<MinutiaPoint>> ParseMinutiae(
        ImmutableArray<byte> data, ref int offset, byte count)
    {
        var minutiae = new List<MinutiaPoint>();
        
        for (int i = 0; i < count; i++)
        {
            if (offset + 6 > data.Length)
                return Result.Failure<ImmutableArray<MinutiaPoint>>("Insufficient data for minutia point");
                
            // Type and X coordinate (2 bytes combined)
            var typeAndX = ReadUInt16BigEndian(data, offset);
            offset += 2;
            var minutiaType = (byte)((typeAndX >> 14) & 0x03);
            var xCoordinate = (ushort)(typeAndX & 0x3FFF);
            
            // Reserved and Y coordinate (2 bytes combined)
            var reservedAndY = ReadUInt16BigEndian(data, offset);
            offset += 2;
            var yCoordinate = (ushort)(reservedAndY & 0x3FFF);
            
            // Angle (1 byte)
            var angle = data[offset++];
            
            // Quality (1 byte)
            var quality = data[offset++];
            
            var minutiaResult = MinutiaPoint.Create(
                minutiaType,
                xCoordinate,
                yCoordinate,
                angle,
                quality);
                
            if (minutiaResult.IsFailure)
                return Result.Failure<ImmutableArray<MinutiaPoint>>(minutiaResult.Error);
                
            minutiae.Add(minutiaResult.Value);
        }
        
        return Result.Success(minutiae.ToImmutableArray());
    }
    
    // Helper methods for reading big-endian values
    
    private static ushort ReadUInt16BigEndian(ImmutableArray<byte> data, int offset)
    {
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }
    
    private static byte[] ReadBytes(ImmutableArray<byte> data, int offset, int count)
    {
        var result = new byte[count];
        for (int i = 0; i < count; i++)
            result[i] = data[offset + i];
        return result;
    }
    
    private static (uint recordLength, int bytesConsumed) ReadRecordLength(ImmutableArray<byte> data, int offset)
    {
        if (offset + 2 > data.Length)
            throw new ArgumentException("Insufficient data for record length");
            
        var firstTwoBytes = ReadUInt16BigEndian(data, offset);
        
        // If first two bytes are 0x0000, then it's a 6-byte format
        if (firstTwoBytes == 0x0000)
        {
            if (offset + 6 > data.Length)
                throw new ArgumentException("Insufficient data for 6-byte record length");
                
            // Read the next 4 bytes as the actual length
            var length = ReadUInt32BigEndian(data, offset + 2);
            return (length, 6);
        }
        else
        {
            // 2-byte format
            return (firstTwoBytes, 2);
        }
    }
    
    private static uint ReadUInt32BigEndian(ImmutableArray<byte> data, int offset)
    {
        return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }
}