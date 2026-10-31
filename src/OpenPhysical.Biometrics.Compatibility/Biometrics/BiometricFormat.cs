namespace OpenPhysical.Domains.Biometrics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Common;

/// <summary>
/// Represents an immutable biometric data format specification.
/// </summary>
/// <remarks>
/// Biometric formats define how biometric data is encoded and structured,
/// typically identified by OIDs in standards like CBEFF.
/// </remarks>
public sealed record BiometricFormat
{
    /// <summary>
    /// Gets the OID that identifies this format.
    /// </summary>
    public Oid FormatOid { get; }

    /// <summary>
    /// Gets the human-readable name of the format.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the format owner identifier.
    /// </summary>
    public ushort FormatOwner { get; }

    /// <summary>
    /// Gets the format type identifier.
    /// </summary>
    public ushort FormatType { get; }

    /// <summary>
    /// Initializes a new instance of the BiometricFormat record.
    /// </summary>
    private BiometricFormat(Oid formatOid, string name, ushort formatOwner, ushort formatType)
    {
        FormatOid = formatOid;
        Name = name;
        FormatOwner = formatOwner;
        FormatType = formatType;
    }

    /// <summary>
    /// Creates a new biometric format.
    /// </summary>
    /// <param name="oidValue">The OID string value.</param>
    /// <param name="name">The format name.</param>
    /// <param name="formatOwner">The format owner identifier.</param>
    /// <param name="formatType">The format type identifier.</param>
    /// <returns>A Result containing the BiometricFormat or an error.</returns>
    public static Result<BiometricFormat> Create(
        string oidValue,
        string name,
        ushort formatOwner,
        ushort formatType
    ) =>
        Result
            .Success()
            .Ensure(() => !string.IsNullOrWhiteSpace(name), "Format name cannot be null or empty")
            .Ensure(() => name.Length <= 100, "Format name too long (max 100 characters)")
            .Bind(() => Oid.Create(oidValue))
            .Map(oid => new BiometricFormat(oid, name, formatOwner, formatType));

    /// <summary>
    /// Common biometric format definitions.
    /// </summary>
    public static class WellKnown
    {
        /// <summary>
        /// INCITS 378 fingerprint minutiae format.
        /// </summary>
        public static BiometricFormat Incits378Minutiae { get; } =
            Create(
                "2.16.840.1.101.3.7.2.16.1",
                "INCITS 378 Fingerprint Minutiae",
                0x001B, // INCITS M1
                0x0201 // Fingerprint minutiae
            ).Value;

        /// <summary>
        /// INCITS 381 fingerprint image format.
        /// </summary>
        public static BiometricFormat Incits381Image { get; } =
            Create(
                "2.16.840.1.101.3.7.2.16.2",
                "INCITS 381 Fingerprint Image",
                0x001B, // INCITS M1
                0x0401 // Fingerprint image
            ).Value;

        /// <summary>
        /// INCITS 385 face image format.
        /// </summary>
        public static BiometricFormat Incits385FaceImage { get; } =
            Create(
                "2.16.840.1.101.3.7.2.16.3",
                "INCITS 385 Face Image",
                0x001B, // INCITS M1
                0x0501 // Face image
            ).Value;

        /// <summary>
        /// ISO/IEC 19794-2 fingerprint minutiae format.
        /// </summary>
        public static BiometricFormat Iso19794Part2Minutiae { get; } =
            Create(
                "1.2.840.10040.2.1",
                "ISO/IEC 19794-2 Fingerprint Minutiae",
                0x0101, // ISO/IEC JTC 1/SC 37
                0x0001 // Fingerprint minutiae
            ).Value;

        /// <summary>
        /// ISO/IEC 19794-5 face image format.
        /// </summary>
        public static BiometricFormat Iso19794Part5FaceImage { get; } =
            Create(
                "1.2.840.10040.2.5",
                "ISO/IEC 19794-5 Face Image",
                0x0101, // ISO/IEC JTC 1/SC 37
                0x0005 // Face image
            ).Value;

        /// <summary>
        /// PIV patron format A for facial images.
        /// </summary>
        public static BiometricFormat PivPatronFormatA { get; } =
            Create(
                "2.16.840.1.101.3.7.2.16.10",
                "PIV Patron Format A",
                0x001B, // INCITS M1
                0x0501 // Face image
            ).Value;

        /// <summary>
        /// PIV patron format B for facial images.
        /// </summary>
        public static BiometricFormat PivPatronFormatB { get; } =
            Create("2.16.840.1.101.3.7.2.16.11", "PIV Patron Format B", 0x0101, 0x0009).Value;
    }

    /// <summary>
    /// Returns the string representation of this biometric format.
    /// </summary>
    public override string ToString() => $"{Name} ({FormatOid})";
}
