namespace OpenPhysical.Domains.Cbeff.Formats.Incits378;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Core information type per INCITS 378-2004 Section 6.6.3.1.
/// Indicates whether angle information is included for core points.
/// Stored in bits 7-6 of the core count byte.
/// </summary>
public enum CoreInformationType : byte
{
    /// <summary>
    /// No angular information is relevant for the core type (0x00).
    /// Core points will only have position (x,y) without angle.
    /// "If this field is '00', then the angle fields shall not be present for the cores."
    /// </summary>
    NoAngle = 0x00,

    /// <summary>Compatibility alias for a core without angular information.</summary>
    NoAdditionalInfo = NoAngle,

    /// <summary>
    /// Core has angular information (0x01).
    /// Core points will have position (x,y) and angle.
    /// "The bits '01' will indicate that the core has angular information."
    /// </summary>
    HasAngle = 0x01,
}
