namespace OpenPhysical.Biometrics.Core.Abstractions.Metadata;

using System.Collections.Immutable;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Images;

/// <summary>
/// Interface for extracting metadata specifically from JPEG images.
/// </summary>
public interface IJpegMetadataExtractor : IImageMetadataExtractor
{
    /// <summary>
    /// Extracts JPEG-specific metadata including EXIF data.
    /// </summary>
    new Task<Result<JpegMetadata>> ExtractAsync(ImmutableArray<byte> imageData);
    
    /// <summary>
    /// Extracts JPEG-specific metadata including EXIF data synchronously.
    /// </summary>
    new Result<JpegMetadata> Extract(ImmutableArray<byte> imageData);
    
    /// <summary>
    /// Extracts only EXIF data from JPEG image.
    /// </summary>
    Result<ExifData> ExtractExif(ImmutableArray<byte> imageData);
}