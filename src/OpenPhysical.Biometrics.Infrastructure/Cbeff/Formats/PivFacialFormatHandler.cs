namespace OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Core.Domain;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;
using System.Collections.Immutable;

/// <summary>
/// Format handler for PIV facial images (INCITS 385) using the new generic architecture.
/// </summary>
[BiometricFormat(0x03, 0x001B, 0x0501, "PIV Facial Image (INCITS 385)")]
public sealed class PivFacialFormatHandler : BiometricFormatHandlerBase<FacialImageRecord>
{
    /// <inheritdoc />
    public override string FormatName => "PIV Facial Image (INCITS 385)";
    
    /// <inheritdoc />
    public override byte PatronFormat => 0x03; // PIV patron format
    
    /// <inheritdoc />
    public override ushort BdbFormatOwner => 0x001B; // INCITS
    
    /// <inheritdoc />
    public override ushort BdbFormatType => 0x0501; // Facial image
    
    /// <inheritdoc />
    public override Result<FacialImageRecord> ParseBiometricData(ImmutableArray<byte> data)
    {
        return Incits385Parser.ParseIncits385Record(data);
    }
    
    /// <inheritdoc />
    public override Result<ImmutableArray<byte>> SerializeBiometricData(FacialImageRecord record)
    {
        return SerializeIncits385Record(record);
    }
    
    /// <inheritdoc />
    public override Result ValidateBiometricData(ImmutableArray<byte> data)
    {
        return ParseBiometricData(data)
            .Bind(record => record.ValidateImageFormat())
            .Map(_ => Result.Success())
            .GetValueOrDefault(Result.Success());
    }
    
    private static Result<ImmutableArray<byte>> SerializeIncits385Record(FacialImageRecord record)
    {
        var buffer = new List<byte>();
        
        // Serialize header
        buffer.AddRange(GetBytesFromAscii(record.Header.FormatIdentifier));
        buffer.AddRange(GetBytesFromAscii(record.Header.VersionNumber));
        buffer.AddRange(GetBytes(record.Header.RecordLength));
        buffer.AddRange(GetBytes(record.Header.NumberOfFaces));
        
        // Serialize facial information
        buffer.AddRange(GetBytes(record.FacialInfo.FaceImageBlockLength));
        buffer.AddRange(GetBytes(record.FacialInfo.NumberOfFeaturePoints));
        buffer.Add(record.FacialInfo.Gender);
        buffer.Add(record.FacialInfo.EyeColor);
        buffer.Add(record.FacialInfo.HairColor);
        buffer.AddRange(GetBytes24(record.FacialInfo.FeatureMask));
        buffer.AddRange(GetBytes(record.FacialInfo.Expression));
        buffer.AddRange(GetBytes24(record.FacialInfo.PoseAngle));
        buffer.AddRange(GetBytes24(record.FacialInfo.PoseAngleUncertainty));
        
        // Serialize feature points
        foreach (var point in record.FeaturePoints)
        {
            buffer.Add(point.FeatureType);
            buffer.Add(point.FeaturePointId);
            buffer.AddRange(GetBytes(point.HorizontalPosition));
            buffer.AddRange(GetBytes(point.VerticalPosition));
            buffer.AddRange(GetBytes(point.Reserved));
        }
        
        // Serialize image information
        buffer.Add(record.ImageInfo.FacialImageType);
        buffer.Add(record.ImageInfo.ImageDataType);
        buffer.AddRange(GetBytes(record.ImageInfo.Width));
        buffer.AddRange(GetBytes(record.ImageInfo.Height));
        buffer.Add(record.ImageInfo.ImageColorSpace);
        buffer.Add(record.ImageInfo.SourceType);
        buffer.AddRange(GetBytes(record.ImageInfo.DeviceType));
        buffer.AddRange(GetBytes(record.ImageInfo.Quality));
        
        // Add image data
        buffer.AddRange(record.ImageData);
        
        return Result.Success(buffer.ToImmutableArray());
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
    
    private static byte[] GetBytes(ushort value)
    {
        return new[]
        {
            (byte)(value >> 8),
            (byte)value
        };
    }
    
    private static byte[] GetBytes24(uint value)
    {
        return new[]
        {
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value
        };
    }
    
    private static byte[] GetBytesFromAscii(string value)
    {
        if (value.Length != 4)
            throw new ArgumentException("ASCII string must be exactly 4 characters", nameof(value));
            
        return System.Text.Encoding.ASCII.GetBytes(value);
    }
}