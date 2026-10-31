using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Formats.Faces;

/// <summary>Contains facial metadata encoded by INCITS 385-2004.</summary>
/// <param name="Gender">Encoded gender value.</param>
/// <param name="EyeColor">Encoded eye-colour value.</param>
/// <param name="HairColor">Encoded hair-colour value.</param>
/// <param name="FeatureMask">24-bit feature mask.</param>
/// <param name="Expression">Encoded expression value.</param>
/// <param name="PoseAngle">Three packed pose-angle bytes, retained in wire order.</param>
/// <param name="PoseUncertainty">Three packed pose-uncertainty bytes, retained in wire order.</param>
public sealed record FaceInformation(
    byte Gender,
    byte EyeColor,
    byte HairColor,
    uint FeatureMask,
    ushort Expression,
    uint PoseAngle,
    uint PoseUncertainty
);

/// <summary>Describes one facial feature point.</summary>
/// <param name="Type">Feature-point encoding type.</param>
/// <param name="Identifier">Feature point identifier within that encoding.</param>
/// <param name="X">Horizontal image coordinate.</param>
/// <param name="Y">Vertical image coordinate.</param>
public sealed record FaceFeaturePoint(byte Type, byte Identifier, ushort X, ushort Y);

/// <summary>Describes the encoded face image without decoding its pixels.</summary>
/// <param name="FaceImageType">Encoded facial image type.</param>
/// <param name="ImageDataType">Zero for JPEG or one for JPEG 2000.</param>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
/// <param name="ColorSpace">Encoded colour-space value.</param>
/// <param name="SourceType">Encoded capture-source value.</param>
/// <param name="DeviceType">Capture-device identifier.</param>
/// <param name="Quality">The complete unsigned 16-bit quality field, including standard sentinel values.</param>
public sealed record FaceImageInformation(
    byte FaceImageType,
    byte ImageDataType,
    ushort Width,
    ushort Height,
    byte ColorSpace,
    byte SourceType,
    ushort DeviceType,
    ushort Quality
);

/// <summary>Contains one facial image block and its immutable metadata.</summary>
/// <param name="Information">Facial metadata.</param>
/// <param name="FeaturePoints">Feature points in wire order.</param>
/// <param name="ImageInformation">Encoded-image metadata.</param>
/// <param name="ImageData">Compressed image bytes, copied from their bounded block during parsing.</param>
public sealed record FaceImage(
    FaceInformation Information,
    ImmutableArray<FaceFeaturePoint> FeaturePoints,
    FaceImageInformation ImageInformation,
    ImmutableArray<byte> ImageData
);

/// <summary>Contains all image blocks in one INCITS 385-2004 facial record.</summary>
/// <param name="Images">Image blocks in wire order.</param>
public sealed record FaceRecord(ImmutableArray<FaceImage> Images);
