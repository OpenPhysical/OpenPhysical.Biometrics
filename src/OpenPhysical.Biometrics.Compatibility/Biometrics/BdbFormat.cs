namespace OpenPhysical.Domains.Biometrics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Biometric Data Block (BDB) format identifier.
/// </summary>
/// <remarks>
/// Combines format owner and format type as defined in ISO/IEC 19785-1:2015.
/// Used to identify the specific biometric data format within CBEFF structures.
/// </remarks>
public readonly record struct BdbFormat
{
    /// <summary>
    /// INCITS 378 fingerprint minutiae format.
    /// Owner: 0x001B (INCITS), Type: 0x0201 (Minutiae).
    /// </summary>
    public static readonly BdbFormat IncitsFingerprintMinutiae = new(0x001B, 0x0201);

    /// <summary>
    /// INCITS 385 facial image format.
    /// Owner: 0x001B (INCITS), Type: 0x0501 (Face).
    /// </summary>
    public static readonly BdbFormat IncitsFacialImage = new(0x001B, 0x0501);

    /// <summary>
    /// INCITS 381 fingerprint image format.
    /// Owner: 0x001B (INCITS), Type: 0x0401 (Finger Image).
    /// </summary>
    public static readonly BdbFormat IncitsFingerprintImage = new(0x001B, 0x0401);

    /// <summary>
    /// ISO/IEC 19794-2 fingerprint minutiae format.
    /// Owner: 0x0101 (ISO/IEC JTC 1/SC 37), Type: 0x0201 (Minutiae).
    /// </summary>
    public static readonly BdbFormat IsoFingerprintMinutiae = new(0x0101, 0x0201);

    /// <summary>
    /// ISO/IEC 19794-5 face image format.
    /// Owner: 0x0101 (ISO/IEC JTC 1/SC 37), Type: 0x0501 (Face).
    /// </summary>
    public static readonly BdbFormat IsoFaceImage = new(0x0101, 0x0501);

    /// <summary>
    /// Gets the format owner identifier.
    /// </summary>
    /// <remarks>
    /// Identifies the organization that defined the format.
    /// Common values:
    /// - 0x001B: INCITS (InterNational Committee for Information Technology Standards)
    /// - 0x0101: ISO/IEC JTC 1/SC 37
    /// </remarks>
    public ushort Owner { get; }

    /// <summary>
    /// Gets the format type identifier.
    /// </summary>
    /// <remarks>
    /// Identifies the type of biometric data.
    /// Common values:
    /// - 0x0201: Fingerprint minutiae
    /// - 0x0401: Fingerprint image
    /// - 0x0501: Face image
    /// </remarks>
    public ushort Type { get; }

    /// <summary>
    /// Initializes a new instance of the BdbFormat struct.
    /// </summary>
    /// <param name="owner">The format owner identifier.</param>
    /// <param name="type">The format type identifier.</param>
    public BdbFormat(ushort owner, ushort type)
    {
        Owner = owner;
        Type = type;
    }

    /// <summary>
    /// Gets the owner organization name.
    /// </summary>
    public string GetOwnerName() =>
        Owner switch
        {
            0x001B => "INCITS",
            0x0101 => "ISO/IEC JTC 1/SC 37",
            0x0000 => "Not specified",
            _ => $"Unknown ({Owner:X4})",
        };

    /// <summary>
    /// Gets the format type description.
    /// </summary>
    public string GetTypeName() =>
        Type switch
        {
            0x0201 => "Fingerprint minutiae",
            0x0401 => "Fingerprint image",
            0x0501 => "Face image",
            0x0601 => "Iris image",
            0x0701 => "Signature/sign behavioral data",
            0x0801 => "Voice data",
            _ => $"Unknown ({Type:X4})",
        };

    /// <summary>
    /// Returns the string representation of this BDB format.
    /// </summary>
    public override string ToString() => $"{GetOwnerName()} - {GetTypeName()}";

    /// <summary>
    /// Determines whether this format is for fingerprint data.
    /// </summary>
    public bool IsFingerprint => Type is 0x0201 or 0x0401;

    /// <summary>
    /// Determines whether this format is for facial data.
    /// </summary>
    public bool IsFacial => Type == 0x0501;

    /// <summary>
    /// Determines whether this format is for minutiae data.
    /// </summary>
    public bool IsMinutiae => Type == 0x0201;

    /// <summary>
    /// Determines whether this format is for image data.
    /// </summary>
    public bool IsImage => Type is 0x0401 or 0x0501 or 0x0601;
}
