namespace OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;
using System.Collections.Immutable;

/// <summary>
/// Format handler for PIV facial images (INCITS 385).
/// </summary>
public sealed class PivFacialFormat : ICbeffFormat
{
    /// <inheritdoc />
    public string FormatName => "PIV Facial Image (INCITS 385)";
    
    /// <inheritdoc />
    public byte PatronFormat => 0x03; // PIV patron format
    
    /// <inheritdoc />
    public ushort BdbFormatOwner => 0x001B; // INCITS
    
    /// <inheritdoc />
    public ushort BdbFormatType => 0x0501; // Facial image
    
    /// <inheritdoc />
    public Result<BiometricRecord> ParseBiometricData(ImmutableArray<byte> data)
    {
        return ParseIncits385Record(data)
            .Map(record => (BiometricRecord)new Incits385BiometricRecord(data, record));
    }
    
    /// <inheritdoc />
    public Result<ImmutableArray<byte>> SerializeBiometricData(BiometricRecord record)
    {
        if (record is not Incits385BiometricRecord incits385Record)
            return Result.Failure<ImmutableArray<byte>>("Record is not an INCITS 385 facial record");
            
        return SerializeIncits385Record(incits385Record.FacialRecord);
    }
    
    /// <inheritdoc />
    public Result ValidateBiometricData(ImmutableArray<byte> data)
    {
        return ParseIncits385Record(data)
            .Bind(record => record.ValidateImageFormat())
            .Map(_ => Result.Success())
            .GetValueOrDefault(Result.Success());
    }
    
    public static Result<FacialImageRecord> ParseIncits385Record(ImmutableArray<byte> data)
    {
        if (data.IsDefaultOrEmpty)
            return Result.Failure<FacialImageRecord>("Biometric data is empty");
            
        if (data.Length < 14) // Minimum header size
            return Result.Failure<FacialImageRecord>("Biometric data too small for INCITS 385 header");
            
        var offset = 0;
        
        // Parse facial record header (14 bytes)
        return ParseFacialRecordHeader(data, ref offset)
            .Bind(header => ParseFacialInformation(data, ref offset)
                .Bind(facialInfo => ParseFeaturePoints(data, ref offset, facialInfo.NumberOfFeaturePoints)
                    .Bind(featurePoints => ParseImageInformation(data, ref offset)
                        .Bind(imageInfo => ExtractImageData(data, offset, header, facialInfo, featurePoints, imageInfo)))));
    }
    
    private static Result<FacialRecordHeader> ParseFacialRecordHeader(ImmutableArray<byte> data, ref int offset)
    {
        if (offset + 14 > data.Length)
            return Result.Failure<FacialRecordHeader>("Insufficient data for facial record header");
            
        var formatIdBytes = ReadUInt32BigEndian(data, offset);
        offset += 4;
        
        var versionBytes = ReadUInt32BigEndian(data, offset);
        offset += 4;
        
        var recordLength = ReadUInt32BigEndian(data, offset);
        offset += 4;
        
        var numberOfFaces = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        return FacialRecordHeader.CreateFromBytes(formatIdBytes, versionBytes, recordLength, numberOfFaces);
    }
    
    private static Result<FacialInformation> ParseFacialInformation(ImmutableArray<byte> data, ref int offset)
    {
        if (offset + 24 > data.Length)
            return Result.Failure<FacialInformation>("Insufficient data for facial information");
            
        var faceImageBlockLength = ReadUInt32BigEndian(data, offset);
        offset += 4;
        
        var numberOfFeaturePoints = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        var gender = data[offset++];
        var eyeColor = data[offset++];
        var hairColor = data[offset++];
        
        var featureMask = ReadUInt24BigEndian(data, offset);
        offset += 3;
        
        var expression = ReadUInt16BigEndian(data, offset);
        offset += 2;
        
        var poseAngle = ReadUInt24BigEndian(data, offset);
        offset += 3;
        
        var poseAngleUncertainty = ReadUInt24BigEndian(data, offset);
        offset += 3;
        
        return FacialInformation.Create(
            faceImageBlockLength,
            numberOfFeaturePoints,
            gender,
            eyeColor,
            hairColor,
            featureMask,
            expression,
            poseAngle,
            poseAngleUncertainty);
    }
    
    private static Result<ImmutableArray<FeaturePoint>> ParseFeaturePoints(
        ImmutableArray<byte> data, ref int offset, ushort count)
    {
        var featurePoints = new List<FeaturePoint>();
        
        for (int i = 0; i < count; i++)
        {
            if (offset + 8 > data.Length)
                return Result.Failure<ImmutableArray<FeaturePoint>>("Insufficient data for feature points");
                
            var featureType = data[offset++];
            var featurePointId = data[offset++];
            var horizontalPos = ReadUInt16BigEndian(data, offset);
            offset += 2;
            var verticalPos = ReadUInt16BigEndian(data, offset);
            offset += 2;
            var reserved = ReadUInt16BigEndian(data, offset);
            offset += 2;
            
            var pointResult = FeaturePoint.Create(featureType, featurePointId, horizontalPos, verticalPos, reserved);
            if (pointResult.IsFailure)
                return Result.Failure<ImmutableArray<FeaturePoint>>(pointResult.Error);
                
            featurePoints.Add(pointResult.Value);
        }
        
        return Result.Success(featurePoints.ToImmutableArray());
    }
    
    private static Result<ImageInformation> ParseImageInformation(ImmutableArray<byte> data, ref int offset)
    {
        if (offset + 12 > data.Length)
            return Result.Failure<ImageInformation>("Insufficient data for image information");
            
        var facialImageType = data[offset++];
        var imageDataType = data[offset++];
        var width = ReadUInt16BigEndian(data, offset);
        offset += 2;
        var height = ReadUInt16BigEndian(data, offset);
        offset += 2;
        var imageColorSpace = data[offset++];
        var sourceType = data[offset++];
        var deviceType = ReadUInt16BigEndian(data, offset);
        offset += 2;
        var quality = data[offset++]; // Quality is 1 byte per INCITS 385
        var qualityPadding = data[offset++]; // 1 byte padding
        
        return ImageInformation.Create(
            facialImageType,
            imageDataType,
            width,
            height,
            imageColorSpace,
            sourceType,
            deviceType,
            quality);
    }
    
    private static Result<FacialImageRecord> ExtractImageData(
        ImmutableArray<byte> data,
        int offset,
        FacialRecordHeader header,
        FacialInformation facialInfo,
        ImmutableArray<FeaturePoint> featurePoints,
        ImageInformation imageInfo)
    {
        var imageDataLength = data.Length - offset;
        if (imageDataLength <= 0)
            return Result.Failure<FacialImageRecord>("No image data found");
            
        var imageData = data.Skip(offset).ToImmutableArray();
        
        return FacialImageRecord.Create(header, facialInfo, featurePoints, imageInfo, imageData);
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
    
    // Helper methods for reading big-endian values
    
    private static uint ReadUInt32BigEndian(ImmutableArray<byte> data, int offset)
    {
        return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | 
                      (data[offset + 2] << 8) | data[offset + 3]);
    }
    
    private static ushort ReadUInt16BigEndian(ImmutableArray<byte> data, int offset)
    {
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }
    
    private static uint ReadUInt24BigEndian(ImmutableArray<byte> data, int offset)
    {
        return (uint)((data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2]);
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