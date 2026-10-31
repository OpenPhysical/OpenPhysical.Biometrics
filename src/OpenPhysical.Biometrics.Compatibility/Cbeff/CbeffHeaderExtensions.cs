namespace OpenPhysical.Domains.Cbeff;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Biometrics;
using Common;
using CSharpFunctionalExtensions;

/// <summary>
/// Extension methods for CbeffHeader to integrate with biometric domain models.
/// </summary>
public static class CbeffHeaderExtensions
{
    /// <summary>
    /// Gets the biometric modality based on the BiometricType field.
    /// </summary>
    /// <param name="header">The CBEFF header</param>
    /// <returns>The biometric modality</returns>
    public static BiometricModality GetBiometricModality(this CbeffHeader header)
    {
        // PIV uses specific biometric type values
        // The biometric type is a 4-byte value, typically only the last byte is used
        var modalityByte = (byte)(header.BiometricType & 0xFF);

        return modalityByte switch
        {
            0x02 => BiometricModality.Face,
            0x08 => BiometricModality.Fingerprint,
            0x10 => BiometricModality.Iris,
            0x20 => BiometricModality.Voice,
            0x40 => BiometricModality.Signature,
            _ => BiometricModality.Create($"UNKNOWN_0x{modalityByte:X2}").Value,
        };
    }

    /// <summary>
    /// Gets the biometric format based on the BDB format fields.
    /// </summary>
    /// <param name="header">The CBEFF header</param>
    /// <returns>The biometric format</returns>
    public static BiometricFormat GetBiometricFormat(this CbeffHeader header) =>
        // Map common format owner/type combinations used in PIV
        (header.BdbFormatOwner, header.BdbFormatType) switch
        {
            (0x001B, 0x0201) => BiometricFormat.WellKnown.Incits378Minutiae,
            (0x001B, 0x0501) => BiometricFormat.WellKnown.Incits385FaceImage,
            (0x0101, 0x0008) => BiometricFormat.WellKnown.PivPatronFormatA,
            (0x0101, 0x0009) => BiometricFormat.WellKnown.PivPatronFormatB,
            _ => BiometricFormat
                .Create(
                    $"2.16.840.1.101.3.7.2.{header.BdbFormatOwner}.{header.BdbFormatType}",
                    "Unknown Format",
                    header.BdbFormatOwner,
                    header.BdbFormatType
                )
                .Value,
        };

    /// <summary>
    /// Checks if this header indicates the CBEFF object is signed.
    /// </summary>
    /// <param name="header">The CBEFF header</param>
    /// <returns>True if signed (SbLength > 0), false otherwise</returns>
    public static bool IsSigned(this CbeffHeader header) => header.SbLength > 0;

    /// <summary>
    /// Checks if this header is valid for PIV use.
    /// </summary>
    /// <param name="header">The CBEFF header</param>
    /// <returns>True if valid PIV header, false otherwise</returns>
    public static bool IsValidPivHeader(this CbeffHeader header)
    {
        // PIV requires patron header version 0x03
        if (header.PatronHeaderVersion != 0x03)
        {
            return false;
        }

        // Security options must be 0x08 (integrity), 0x0C (integrity + confidentiality), or 0x0D (signed)
        if (
            header.SbhSecurityOptions != 0x08
            && header.SbhSecurityOptions != 0x0C
            && header.SbhSecurityOptions != 0x0D
        )
        {
            return false;
        }

        // BDB format owner must be 0x001B (INCITS TC M1) for standard formats
        // or 0x0101 for PIV patron formats
        if (header.BdbFormatOwner != 0x001B && header.BdbFormatOwner != 0x0101)
        {
            return false;
        }

        // Reserved bytes must be zero
        foreach (var b in header.Reserved)
        {
            if (b != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a human-readable description of the security options.
    /// </summary>
    /// <param name="header">The CBEFF header</param>
    /// <returns>Description of the security options</returns>
    public static string GetSecurityOptionsDescription(this CbeffHeader header) =>
        header.SbhSecurityOptions switch
        {
            0x00 => "No security",
            0x08 => "Integrity",
            0x0C => "Integrity and confidentiality",
            0x0D => "Signed plaintext",
            _ => $"Unknown (0x{header.SbhSecurityOptions:X2})",
        };

    /// <summary>
    /// Gets the quality description based on the quality value.
    /// </summary>
    /// <param name="header">The CBEFF header</param>
    /// <returns>Quality description</returns>
    public static string GetQualityDescription(this CbeffHeader header) =>
        // PIV uses special values 254 and 255 which when cast to sbyte become -2 and -1
        header.Quality switch
        {
            -2 => "Permanently unusable (PIV)", // 254 as unsigned byte = -2 as sbyte
            -1 => "Temporarily unusable (PIV)", // 255 as unsigned byte = -1 as sbyte
            >= 0 and <= 100 => $"{header.Quality}%",
            _ => $"Invalid quality value ({header.Quality})",
        };
}
