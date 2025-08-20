namespace OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;
using System.Collections.Immutable;

/// <summary>
/// Format handler for PIV fingerprint templates (INCITS 378) using the new generic architecture.
/// </summary>
[BiometricFormat(0x03, 0x001B, 0x0201, "PIV Fingerprint Minutiae (INCITS 378)")]
public sealed class PivFingerprintFormatHandler : IBiometricFormatHandler<FingerprintRecord>
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
    public Result<FingerprintRecord> ParseBiometricData(ImmutableArray<byte> data)
    {
        return Incits378Parser.ParseIncits378Record(data);
    }
    
    /// <inheritdoc />
    public Result<ImmutableArray<byte>> SerializeBiometricData(FingerprintRecord record)
    {
        return SerializeIncits378Record(record);
    }
    
    /// <inheritdoc />
    public Result ValidateBiometricData(ImmutableArray<byte> data)
    {
        return ParseBiometricData(data)
            .Bind(record => record.ValidateFormat());
    }
    
    private static Result<ImmutableArray<byte>> SerializeIncits378Record(FingerprintRecord record)
    {
        var buffer = new List<byte>();
        
        // Serialize general record header
        buffer.AddRange(record.Header.FormatIdentifier);
        buffer.AddRange(record.Header.VersionNumber);
        
        // Serialize record length (2 or 6 bytes depending on value)
        if (record.Header.RecordLength <= 65535)
        {
            // 2-byte format
            buffer.AddRange(GetBytes((ushort)record.Header.RecordLength));
        }
        else
        {
            // 6-byte format: 0x0000 followed by 4-byte length
            buffer.AddRange(GetBytes((ushort)0));
            buffer.AddRange(GetBytes(record.Header.RecordLength));
        }
        buffer.AddRange(GetBytes(record.Header.ProductOwner));
        buffer.AddRange(GetBytes(record.Header.ProductType));
        buffer.AddRange(GetBytes(record.Header.CaptureEquipmentCompliance));
        buffer.AddRange(GetBytes(record.Header.ImageSizeX));
        buffer.AddRange(GetBytes(record.Header.ImageSizeY));
        buffer.AddRange(GetBytes(record.Header.XResolution));
        buffer.AddRange(GetBytes(record.Header.YResolution));
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
    
    private static byte[] GetBytes(ushort value)
    {
        return new[]
        {
            (byte)(value >> 8),
            (byte)value
        };
    }
    
    private static byte[] GetBytes(uint value)
    {
        return new[]
        {
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value
        };
    }
}