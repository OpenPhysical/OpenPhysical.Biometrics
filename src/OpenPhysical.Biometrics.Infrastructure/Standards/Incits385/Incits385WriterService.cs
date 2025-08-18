namespace OpenPhysical.Biometrics.Infrastructure.Standards.Incits385;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Standards.Incits385;
using OpenPhysical.Domains.Biometrics;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;

/// <summary>
/// Service for writing INCITS 385 facial image records.
/// </summary>
public sealed class Incits385WriterService : IIncits385Writer
{
    private const string FormatIdentifier = "FAC\0";
    private const string VersionNumber = "010\0";
    private const byte FacialImageType = 0x01; // Full frontal
    private const byte SourceType = 0x02; // Static photograph - digital camera
    
    /// <summary>
    /// Creates an INCITS 385 facial image record from image data and metadata.
    /// </summary>
    public Result<byte[]> CreateFacialRecord(
        byte[] imageData,
        ImageFormat format,
        ImageDimensions dimensions,
        Incits385Metadata metadata)
    {
        if (imageData == null || imageData.Length == 0)
            return Result.Failure<byte[]>("Image data cannot be null or empty");
            
        if (dimensions.Width == 0 || dimensions.Height == 0)
            return Result.Failure<byte[]>("Invalid image dimensions");
            
        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            
            // Calculate sizes
            var featurePointCount = metadata.FeaturePoints.HasValue 
                ? metadata.FeaturePoints.Value.Length 
                : 0;
            var featurePointsSize = featurePointCount * 8; // Each point is 8 bytes
            
            var faceImageBlockLength = 20 + featurePointsSize + 12 + imageData.Length;
            var recordLength = 14 + faceImageBlockLength;
            
            // Write Facial Record Header (14 bytes)
            writer.Write(Encoding.ASCII.GetBytes(FormatIdentifier)); // 4 bytes
            writer.Write(Encoding.ASCII.GetBytes(VersionNumber));    // 4 bytes
            writer.Write(ToBigEndian((uint)recordLength));          // 4 bytes
            writer.Write(ToBigEndian((ushort)1));                   // 2 bytes - number of facial records
            
            // Write Facial Information (20 bytes)
            writer.Write(ToBigEndian((uint)faceImageBlockLength));  // 4 bytes
            writer.Write(ToBigEndian((ushort)featurePointCount));   // 2 bytes
            writer.Write((byte)metadata.Gender);                     // 1 byte
            writer.Write((byte)metadata.EyeColor);                   // 1 byte
            writer.Write((byte)metadata.HairColor);                  // 1 byte
            writer.Write(new byte[3]);                               // 3 bytes - feature mask (24 bits)
            writer.Write(ToBigEndian((ushort)0x0001));              // 2 bytes - expression (neutral)
            writer.Write(new byte[3]);                               // 3 bytes - pose angle
            writer.Write(new byte[3]);                               // 3 bytes - pose angle uncertainty
            
            // Write Feature Points (if any)
            if (metadata.FeaturePoints.HasValue)
            {
                foreach (var point in metadata.FeaturePoints.Value)
                {
                    writer.Write(point.FeatureType);                 // 1 byte
                    writer.Write(new byte[3]);                       // 3 bytes - padding
                    writer.Write(ToBigEndian(point.HorizontalPosition)); // 2 bytes
                    writer.Write(ToBigEndian(point.VerticalPosition));   // 2 bytes
                }
            }
            
            // Write Image Information (12 bytes)
            writer.Write(FacialImageType);                           // 1 byte
            writer.Write((byte)format);                              // 1 byte - image data type
            writer.Write(ToBigEndian((ushort)dimensions.Width));     // 2 bytes
            writer.Write(ToBigEndian((ushort)dimensions.Height));    // 2 bytes
            writer.Write((byte)GetColorSpace(format));               // 1 byte
            writer.Write(SourceType);                                // 1 byte
            writer.Write(ToBigEndian((ushort)0));                   // 2 bytes - device type
            writer.Write(metadata.Quality);                          // 1 byte
            writer.Write((byte)0);                                   // 1 byte - padding
            
            // Write Image Data
            writer.Write(imageData);
            
            return Result.Success(stream.ToArray());
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>($"Error creating INCITS 385 record: {ex.Message}");
        }
    }
    
    private static byte GetColorSpace(ImageFormat format)
    {
        // PIV requires grayscale for JPEG 2000
        return format == ImageFormat.Jpeg2000 ? (byte)0x03 : (byte)0x01; // Grayscale : RGB
    }
    
    private static byte[] ToBigEndian(ushort value)
    {
        return new[] { (byte)(value >> 8), (byte)value };
    }
    
    private static byte[] ToBigEndian(uint value)
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