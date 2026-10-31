namespace OpenPhysical.Domains.Cbeff.Formats.Incits385;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// INCITS 385 eye color codes.
/// </summary>
public enum EyeColor : byte
{
    /// <summary>Eye colour was not supplied.</summary>
    Unspecified = 0x00,

    /// <summary>Eye colour recorded as blue.</summary>
    Blue = 0x01,

    /// <summary>Eye colour recorded as brown.</summary>
    Brown = 0x02,

    /// <summary>Eye colour recorded as gray.</summary>
    Gray = 0x03,

    /// <summary>Eye colour recorded as green.</summary>
    Green = 0x04,

    /// <summary>Eye colour recorded as hazel.</summary>
    Hazel = 0x05,

    /// <summary>Eye colour could not be determined.</summary>
    Unknown = 0x06,
}
