namespace OpenPhysical.Domains.Cbeff;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// CBEFF biometric organization registry identifiers (IBIA ISO/IEC 19785-2).
/// </summary>
public static class CbeffOrganizationRegistry
{
    private static readonly Dictionary<ushort, string> Organizations = new()
    {
        { 0x000F, "National Institute of Standards and Technology (NIST)" },
        { 0x001B, "INCITS Technical Committee M1" },
        { 0x003B, "Aware, Inc." },
        { 0x0052, "OASIS" },
        { 0x0064, "Samsung R&D Institute India" },
        { 0x0066, "Papillon AO" },
        { 0x0067, "Hitachi Beijing Tech Information Systems Ltd." },
        { 0x0069, "Beijing IDWorld Science And Technology Development Co., Ltd" },
        { 0x006A, "Beijing EyeCool Technology Co., Ltd" },
        { 0x0101, "ISO/IEC JTC 1 SC 37 Biometrics" },
        { 0x0102, "ISO/IEC JTC 1 SC 27 IT Security techniques" },
    };

    /// <summary>Returns the registered organization name for a format-owner identifier.</summary>
    public static string GetOrganizationName(ushort identifier) =>
        Organizations.TryGetValue(identifier, out var name) ? name : $"Unknown ({identifier:X4})";
}
