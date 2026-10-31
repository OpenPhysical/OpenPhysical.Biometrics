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
/// INCITS 385 hair color codes.
/// </summary>
public enum HairColor : byte
{
    /// <summary>Hair colour was not supplied.</summary>
    Unspecified = 0x00,

    /// <summary>Hair colour recorded as black.</summary>
    Black = 0x01,

    /// <summary>Hair colour recorded as blonde.</summary>
    Blonde = 0x02,

    /// <summary>Hair colour recorded as brown.</summary>
    Brown = 0x03,

    /// <summary>Hair colour recorded as gray.</summary>
    Gray = 0x04,

    /// <summary>Hair colour recorded as red.</summary>
    Red = 0x05,

    /// <summary>Hair colour recorded as bald.</summary>
    Bald = 0x06,

    /// <summary>Hair colour could not be determined.</summary>
    Unknown = 0x07,
}
