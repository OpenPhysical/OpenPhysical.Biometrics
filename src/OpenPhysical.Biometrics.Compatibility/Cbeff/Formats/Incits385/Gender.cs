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
/// INCITS 385 gender codes.
/// </summary>
public enum Gender : byte
{
    /// <summary>Gender was not supplied.</summary>
    Unspecified = 0x00,

    /// <summary>Gender recorded as male.</summary>
    Male = 0x01,

    /// <summary>Gender recorded as female.</summary>
    Female = 0x02,

    /// <summary>Gender could not be determined.</summary>
    Unknown = 0x03,
}
