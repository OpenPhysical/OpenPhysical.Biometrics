// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Reviewed complete head bounds and shoulder protection in prepared-portrait pixels.</summary>
/// <param name="Left">Inclusive left edge of all hair, head, and ears.</param>
/// <param name="Top">Inclusive top edge of all hair and head.</param>
/// <param name="Right">Exclusive right edge of all hair, head, and ears.</param>
/// <param name="Bottom">Exclusive bottom edge of the complete head.</param>
/// <param name="ChinY">All rows at or below this chin coordinate protect shoulders and clothing.</param>
public sealed record PortraitForegroundProtection(
    int Left,
    int Top,
    int Right,
    int Bottom,
    int ChinY
);
