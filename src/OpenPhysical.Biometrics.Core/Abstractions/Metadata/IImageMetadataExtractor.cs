namespace OpenPhysical.Biometrics.Core.Abstractions.Metadata;

using System.Collections.Immutable;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Images;

/// <summary>
/// Base interface for extracting metadata from image data.
/// </summary>
public interface IImageMetadataExtractor
{
    /// <summary>
    /// Gets the supported image formats.
    /// </summary>
    ImmutableHashSet<ImageFormat> SupportedFormats { get; }
    
    /// <summary>
    /// Determines if this extractor can handle the given image data.
    /// </summary>
    Result<bool> CanExtract(ImmutableArray<byte> imageData);
    
    /// <summary>
    /// Extracts metadata from the image data.
    /// </summary>
    Task<Result<ImageMetadata>> ExtractAsync(ImmutableArray<byte> imageData);
    
    /// <summary>
    /// Extracts metadata from the image data synchronously.
    /// </summary>
    Result<ImageMetadata> Extract(ImmutableArray<byte> imageData);
}