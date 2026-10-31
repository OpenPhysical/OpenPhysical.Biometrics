namespace OpenPhysical.Biometrics.Infrastructure.Standards.Incits385;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Standards.Incits385;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Domains.Biometrics;

/// <summary>Adapts the historical facial writer contract to the canonical INCITS 385 codec.</summary>
public sealed class Incits385WriterService : IIncits385Writer
{
    /// <summary>Encodes the supplied compressed sRGB image and facial metadata in a canonical record.</summary>
    /// <param name="imageData">Original compressed JPEG or JPEG 2000 image bytes.</param>
    /// <param name="format">The compressed image format.</param>
    /// <param name="dimensions">Original image dimensions in pixels.</param>
    /// <param name="metadata">Facial attributes, feature points, and quality.</param>
    /// <returns>The encoded record, or a compatibility validation failure.</returns>
    public Result<byte[]> CreateFacialRecord(
        byte[] imageData,
        ImageFormat format,
        ImageDimensions dimensions,
        Incits385Metadata metadata
    )
    {
        if (imageData is null || imageData.Length == 0)
            return Result.Failure<byte[]>("Image data cannot be null or empty.");
        if (dimensions is null || metadata is null)
            return Result.Failure<byte[]>("Image dimensions and metadata are required.");
        if (!Enum.IsDefined(format))
            return Result.Failure<byte[]>("Select a supported facial image format.");
        try
        {
            var points = metadata.FeaturePoints.HasValue
                ? metadata
                    .FeaturePoints.Value.Select(point => new FaceFeaturePoint(
                        point.FeatureType,
                        point.Reserved,
                        point.HorizontalPosition,
                        point.VerticalPosition
                    ))
                    .ToImmutableArray()
                : ImmutableArray<FaceFeaturePoint>.Empty;
            // The canonical writer owns lengths and big-endian fields. sRGB is
            // preserved for both compressed image formats, including JPEG 2000.
            var image = new FaceImage(
                new FaceInformation(
                    (byte)metadata.Gender,
                    (byte)metadata.EyeColor,
                    (byte)metadata.HairColor,
                    0,
                    1,
                    0,
                    0
                ),
                points,
                new FaceImageInformation(
                    1,
                    (byte)format,
                    checked((ushort)dimensions.Width),
                    checked((ushort)dimensions.Height),
                    1,
                    2,
                    0,
                    metadata.Quality
                ),
                ImmutableArray.Create(imageData)
            );
            return Result.Success(Incits385Codec.Write(new FaceRecord([image])));
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            return Result.Failure<byte[]>("The facial record metadata could not be encoded.");
        }
    }
}
