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
/// Ridge count extraction method per INCITS 378-2004 Section 6.6.2.1 Table 4.
/// Indicates how ridge counts were extracted between minutiae pairs.
/// </summary>
public enum RidgeCountExtractionMethod : byte
{
    /// <summary>
    /// Nonspecific extraction method (0x00).
    /// No assumption shall be made about the method used to extract ridge counts,
    /// nor their order in the record. In particular, the counts may not be between
    /// nearest-neighbor minutiae.
    /// </summary>
    Nonspecific = 0x00,

    /// <summary>Historical alias for the nonspecific extraction method.</summary>
    Specified = Nonspecific,

    /// <summary>
    /// Four-neighbor extraction method using quadrants (0x01).
    /// For each center minutia used, ridge count data was extracted to the nearest
    /// neighboring minutia in four quadrants, and ridge counts for each center minutia
    /// are listed together.
    /// </summary>
    FourNeighbor = 0x01,

    /// <summary>Compatibility alias for four-neighbour quadrant extraction.</summary>
    QuadrantFourNearest = FourNeighbor,

    /// <summary>
    /// Eight-neighbor extraction method using octants (0x02).
    /// For each center minutia used, ridge count data was extracted to the nearest
    /// neighboring minutia in eight octants, and ridge counts for each center minutia
    /// are listed together.
    /// </summary>
    EightNeighbor = 0x02,

    /// <summary>Compatibility alias for eight-neighbour octant extraction.</summary>
    OctantEightNearest = EightNeighbor,
}
