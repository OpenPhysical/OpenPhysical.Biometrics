namespace OpenPhysical.Biometrics.Core.Abstractions.Metadata;

using System.Collections.Immutable;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Images;

/// <summary>
/// Interface for extracting metadata specifically from JPEG 2000 images.
/// </summary>
public interface IJpeg2000MetadataExtractor : IImageMetadataExtractor
{
    /// <summary>
    /// Extracts JPEG 2000-specific metadata including JP2 box information.
    /// </summary>
    new Task<Result<Jpeg2000Metadata>> ExtractAsync(ImmutableArray<byte> imageData);

    /// <summary>
    /// Extracts JPEG 2000-specific metadata including JP2 box information synchronously.
    /// </summary>
    new Result<Jpeg2000Metadata> Extract(ImmutableArray<byte> imageData);

    /// <summary>
    /// Extracts JP2 box structure information.
    /// </summary>
    Result<ImmutableList<Jp2BoxInfo>> ExtractBoxes(ImmutableArray<byte> imageData);
}
