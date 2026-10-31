namespace OpenPhysical.Biometrics.Core.Standards.Incits385;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Biometrics;

/// <summary>
/// Interface for writing INCITS 385 facial image records.
/// </summary>
public interface IIncits385Writer
{
    /// <summary>
    /// Creates an INCITS 385 facial image record from image data and metadata.
    /// </summary>
    /// <param name="imageData">The facial image data (JPEG or JPEG 2000)</param>
    /// <param name="format">The image format</param>
    /// <param name="dimensions">The image dimensions</param>
    /// <param name="metadata">Additional metadata for the record</param>
    /// <returns>Result containing the INCITS 385 formatted binary data or error</returns>
    Result<byte[]> CreateFacialRecord(
        byte[] imageData,
        ImageFormat format,
        ImageDimensions dimensions,
        Incits385Metadata metadata
    );
}

/// <summary>
/// Metadata for creating an INCITS 385 facial image record.
/// </summary>
public sealed record Incits385Metadata(
    byte Quality,
    Gender Gender,
    EyeColor EyeColor,
    HairColor HairColor,
    Maybe<ImmutableArray<FeaturePoint>> FeaturePoints
)
{
    /// <summary>
    /// Creates metadata with default values.
    /// </summary>
    public static Incits385Metadata Default =>
        new(
            Quality: 254, // Not computed
            Gender: Gender.Unspecified,
            EyeColor: EyeColor.Unspecified,
            HairColor: HairColor.Unspecified,
            FeaturePoints: Maybe<ImmutableArray<FeaturePoint>>.None
        );
}

/// <summary>
/// Image format for INCITS 385 records.
/// </summary>
public enum ImageFormat
{
    /// <summary>JPEG format</summary>
    Jpeg = 0,

    /// <summary>JPEG 2000 format</summary>
    Jpeg2000 = 1,
}

/// <summary>
/// Gender values for INCITS 385.
/// </summary>
public enum Gender : byte
{
    /// <summary>Gender not specified</summary>
    Unspecified = 0x00,

    /// <summary>Male</summary>
    Male = 0x01,

    /// <summary>Female</summary>
    Female = 0x02,

    /// <summary>Unknown gender</summary>
    Unknown = 0xFF,
}

/// <summary>
/// Eye color values for INCITS 385.
/// </summary>
public enum EyeColor : byte
{
    /// <summary>Eye color not specified</summary>
    Unspecified = 0x00,

    /// <summary>Blue eyes</summary>
    Blue = 0x01,

    /// <summary>Brown eyes</summary>
    Brown = 0x02,

    /// <summary>Gray eyes</summary>
    Gray = 0x03,

    /// <summary>Green eyes</summary>
    Green = 0x04,

    /// <summary>Hazel eyes</summary>
    Hazel = 0x05,

    /// <summary>Maroon eyes</summary>
    Maroon = 0x06,

    /// <summary>Pink eyes</summary>
    Pink = 0x07,

    /// <summary>Multi-colored eyes</summary>
    MultiColored = 0x08,

    /// <summary>Unknown eye color</summary>
    Unknown = 0xFF,
}

/// <summary>
/// Hair color values for INCITS 385.
/// </summary>
public enum HairColor : byte
{
    /// <summary>Hair color not specified</summary>
    Unspecified = 0x00,

    /// <summary>Bald</summary>
    Bald = 0x01,

    /// <summary>Black hair</summary>
    Black = 0x02,

    /// <summary>Blonde hair</summary>
    Blonde = 0x03,

    /// <summary>Brown hair</summary>
    Brown = 0x04,

    /// <summary>Gray hair</summary>
    Gray = 0x05,

    /// <summary>Red hair</summary>
    Red = 0x06,

    /// <summary>Blue hair</summary>
    Blue = 0x07,

    /// <summary>Green hair</summary>
    Green = 0x08,

    /// <summary>Unknown hair color</summary>
    Unknown = 0xFF,
}

/// <summary>
/// Feature point for facial landmarks.
/// </summary>
public sealed record FeaturePoint(
    byte FeatureType,
    ushort HorizontalPosition,
    ushort VerticalPosition,
    byte Reserved = 0x00
)
{
    /// <summary>
    /// Creates a feature point with validation.
    /// </summary>
    public static Result<FeaturePoint> Create(byte featureType, ushort x, ushort y)
    {
        if (featureType == 0x00)
            return Result.Failure<FeaturePoint>("Feature type cannot be 0x00");

        return Result.Success(new FeaturePoint(featureType, x, y));
    }
}
