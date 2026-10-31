using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Describes a minutia in an INCITS 378-2004 record.</summary>
/// <param name="Type">Zero for other, one for a ridge ending, or two for a bifurcation.</param>
/// <param name="X">Horizontal pixel coordinate measured from the left image edge.</param>
/// <param name="Y">Vertical pixel coordinate measured from the top image edge.</param>
/// <param name="Angle">Counterclockwise angle from positive X in two-degree units, from zero through 179.</param>
/// <param name="Quality">Minutia quality from zero through 100; zero means quality was not reported.</param>
public sealed record FingerprintMinutia(byte Type, ushort X, ushort Y, byte Angle, byte Quality);

/// <summary>Preserves an extended block, including unrecognized vendor data, without changing its bytes.</summary>
/// <param name="Type">Two-byte block type identifier.</param>
/// <param name="Data">Payload bytes following the four-byte type-and-length header.</param>
public sealed record FingerprintExtendedBlock(ushort Type, ImmutableArray<byte> Data);

/// <summary>Contains one finger impression and its associated extended blocks.</summary>
/// <param name="FingerPosition">ANSI finger position code.</param>
/// <param name="ViewNumber">View number from zero through 15.</param>
/// <param name="ImpressionType">Four-bit impression code, such as zero for live plain capture.</param>
/// <param name="Quality">Finger quality from zero through 100, or unavailable status 254 or 255.</param>
/// <param name="Minutiae">Minutiae in the original record order.</param>
/// <param name="ExtendedBlocks">Extended blocks in the original record order.</param>
public sealed record FingerprintView(
    byte FingerPosition,
    byte ViewNumber,
    byte ImpressionType,
    byte Quality,
    ImmutableArray<FingerprintMinutia> Minutiae,
    ImmutableArray<FingerprintExtendedBlock> ExtendedBlocks
);

/// <summary>Contains an immutable INCITS 378-2004 minutiae record with explicit image geometry.</summary>
/// <param name="ProductOwner">Registered extractor product owner.</param>
/// <param name="ProductType">Registered extractor product type.</param>
/// <param name="CaptureEquipment">Packed equipment compliance and identifier.</param>
/// <param name="Width">Capture width in pixels.</param>
/// <param name="Height">Capture height in pixels.</param>
/// <param name="XResolution">Horizontal resolution in pixels per centimetre.</param>
/// <param name="YResolution">Vertical resolution in pixels per centimetre.</param>
/// <param name="Views">Finger views in wire order. PIV policy is applied separately from this interchange model.</param>
public sealed record FingerprintRecord(
    ushort ProductOwner,
    ushort ProductType,
    ushort CaptureEquipment,
    ushort Width,
    ushort Height,
    ushort XResolution,
    ushort YResolution,
    ImmutableArray<FingerprintView> Views
);
