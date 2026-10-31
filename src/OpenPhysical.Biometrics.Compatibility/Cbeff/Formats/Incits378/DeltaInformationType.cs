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
/// Delta information type per INCITS 378-2004 Section 6.6.3.5.
/// Indicates whether angle information is included for delta points.
/// Stored in bits 7-6 of the delta count byte.
/// Note: Convention is opposite of core information type!
/// </summary>
public enum DeltaInformationType : byte
{
    /// <summary>
    /// Delta has angular information (0x00).
    /// Delta points will have position (x,y) and three angles.
    /// "The bits '00' will indicate that the delta has angular information."
    /// Note: This is opposite convention from core type!
    /// </summary>
    HasAngles = 0x00,

    /// <summary>
    /// No angular information is relevant for the delta type (0x01).
    /// Delta points will only have position (x,y) without angles.
    /// "The bits '01' will indicate that no angular information is relevant for the delta type."
    /// "If this field is '00', then the angle fields shall not be present for the deltas."
    /// </summary>
    NoAngles = 0x01,

    /// <summary>Compatibility alias for a delta without angular information.</summary>
    NoAdditionalInfo = NoAngles,
}
