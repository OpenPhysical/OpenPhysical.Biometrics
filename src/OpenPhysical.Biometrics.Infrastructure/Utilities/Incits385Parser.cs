namespace OpenPhysical.Biometrics.Infrastructure.Utilities;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;
using System.Collections.Immutable;

/// <summary>
/// Utility class for parsing INCITS 385 facial image format data.
/// </summary>
public static class Incits385Parser
{
    /// <summary>
    /// Parses INCITS 385 facial image data.
    /// </summary>
    /// <param name="data">The raw biometric data bytes.</param>
    /// <returns>A Result containing the parsed facial image record or an error.</returns>
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
}