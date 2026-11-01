// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;

// The validated general header of one INCITS 378 record, before its views are read.
internal sealed record Incits378Header(
    ushort ProductOwner,
    ushort ProductType,
    ushort CaptureEquipment,
    ushort Width,
    ushort Height,
    ushort XResolution,
    ushort YResolution,
    byte ViewCount
)
{
    internal FingerprintRecord ToRecord(ImmutableArray<FingerprintView> views) =>
        new(
            ProductOwner,
            ProductType,
            CaptureEquipment,
            Width,
            Height,
            XResolution,
            YResolution,
            views
        );
}
