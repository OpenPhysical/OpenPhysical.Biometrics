// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

// Value rules shared by reading and writing INCITS 378 records.
internal static class Incits378Rules
{
    internal static bool IsValidFingerQuality(byte quality) =>
        quality <= 100 || quality is 254 or 255;

    internal static bool IsReservedBlockType(ushort type) =>
        type == 0 || (type > 2 && (type < 0x100 || (type & 0xFF) == 0));
}
