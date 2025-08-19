namespace OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;
using System.Collections.Immutable;
using System.Linq;

/// <summary>
/// Format handler for PIV fingerprint templates (INCITS 378).
/// </summary>
public sealed class PivFingerprintFormat : ICbeffFormat
{
    /// <inheritdoc />
    public string FormatName => "PIV Fingerprint Minutiae (INCITS 378)";
    
    /// <inheritdoc />
    public byte PatronFormat => 0x03; // PIV patron format
    
    /// <inheritdoc />
    public ushort BdbFormatOwner => 0x001B; // INCITS
    
    /// <inheritdoc />
    public ushort BdbFormatType => 0x0201; // Fingerprint minutiae
    
    /// <inheritdoc />
    public Result<BiometricRecord> ParseBiometricData(ImmutableArray<byte> data)
    {
        return ParseIncits378Record(data)
            .Map(record => (BiometricRecord)new Incits378BiometricRecord(data, record));
    }
    
    /// <inheritdoc />
    public Result<ImmutableArray<byte>> SerializeBiometricData(BiometricRecord record)
    {
        if (record is not Incits378BiometricRecord incits378Record)
            return Result.Failure<ImmutableArray<byte>>("Record is not an INCITS 378 fingerprint record");
            
        return SerializeIncits378Record(incits378Record.FingerprintRecord);
    }
    
    /// <inheritdoc />
    public Result ValidateBiometricData(ImmutableArray<byte> data)
    {
        return ParseIncits378Record(data)
            .Bind(record => record.ValidateFormat());
    }
    
    /// <summary>
    /// Parses INCITS 378 fingerprint minutiae data.
    /// </summary>
    public static Result<FingerprintRecord> ParseIncits378Record(ImmutableArray<byte> data)
    {
        if (data.IsDefaultOrEmpty)
            return Result.Failure<FingerprintRecord>("Biometric data is empty");
            
        if (data.Length < 26) // Minimum header size for INCITS 378
            return Result.Failure<FingerprintRecord>("Biometric data too small for INCITS 378 header");
            
        var offset = 0;
        
        // Parse general record header (26 bytes per INCITS 378-2004 Table 6)
        return ParseGeneralRecordHeader(data, ref offset)
            .Bind(header => ParseFingerViewRecords(data, ref offset, header.NumberOfFingerRecords)
                .Map(viewRecords => new FingerprintRecord(header, viewRecords)));
    }
    
    private static Result<GeneralRecordHeader> ParseGeneralRecordHeader(ImmutableArray<byte> data, ref int offset)
    {
        if (offset + 26 > data.Length)
            return Result.Failure<GeneralRecordHeader>("Insufficient data for general record header");
            
        // Format identifier - "FMR\0"
        var formatId = ReadBytes(data, offset, 4);
        offset += 4;
        
        // Version number - " 20\0"  
        var version = ReadBytes(data, offset, 4);
        offset += 4;
        
        // Record length (2 bytes, big-endian)
        var recordLength = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // CBEFF Product Owner (2 bytes, big-endian)
        var productOwner = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // CBEFF Product Type (2 bytes, big-endian)
        var productType = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // Capture equipment compliance (1 byte)
        var captureEquipmentCompliance = data[offset++];
        
        // Capture equipment ID (1 byte)
        var captureEquipmentId = data[offset++];
        
        // Horizontal scan resolution (2 bytes, big-endian, pixels per cm)
        var horizontalResolution = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        // Vertical scan resolution (2 bytes, big-endian, pixels per cm)
        var verticalResolution = ReadUInt16BigEndian(data, offset);
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
            captureEquipmentId,
            horizontalResolution,
            verticalResolution,
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
    
    private static Result<ImmutableArray<byte>> SerializeIncits378Record(FingerprintRecord record)
    {
        var buffer = new List<byte>();
        
        // Serialize general record header
        buffer.AddRange(record.Header.FormatIdentifier);
        buffer.AddRange(record.Header.VersionNumber);
        buffer.AddRange(GetBytes(record.Header.RecordLength));
        buffer.AddRange(GetBytes(record.Header.ProductOwner));
        buffer.AddRange(GetBytes(record.Header.ProductType));
        buffer.Add(record.Header.CaptureEquipmentCompliance);
        buffer.Add(record.Header.CaptureEquipmentId);
        buffer.AddRange(GetBytes(record.Header.HorizontalResolution));
        buffer.AddRange(GetBytes(record.Header.VerticalResolution));
        buffer.Add(record.Header.NumberOfFingerRecords);
        buffer.Add(record.Header.Reserved);
        
        // Serialize finger view records
        foreach (var viewRecord in record.ViewRecords)
        {
            buffer.Add(viewRecord.FingerPosition);
            buffer.Add(viewRecord.ViewNumber);
            buffer.Add(viewRecord.ImpressionType);
            buffer.Add(viewRecord.FingerQuality);
            buffer.Add(viewRecord.NumberOfMinutiae);
            buffer.Add(viewRecord.Reserved);
            
            // Serialize minutiae
            foreach (var minutia in viewRecord.Minutiae)
            {
                // Pack type and X coordinate
                var typeAndX = (ushort)((minutia.MinutiaType << 14) | (minutia.XCoordinate & 0x3FFF));
                buffer.AddRange(GetBytes(typeAndX));
                
                // Pack reserved and Y coordinate  
                var reservedAndY = (ushort)(minutia.YCoordinate & 0x3FFF);
                buffer.AddRange(GetBytes(reservedAndY));
                
                buffer.Add(minutia.Angle);
                buffer.Add(minutia.Quality);
            }
        }
        
        return Result.Success(buffer.ToImmutableArray());
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
    
    private static byte[] GetBytes(ushort value)
    {
        return new[]
        {
            (byte)(value >> 8),
            (byte)value
        };
    }
}