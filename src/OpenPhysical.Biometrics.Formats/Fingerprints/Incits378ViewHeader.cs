// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;

// The four leading bytes of a finger view, before its minutiae and extended data.
internal sealed record Incits378ViewHeader(
    byte FingerPosition,
    byte ViewAndImpression,
    byte Quality,
    byte MinutiaCount
)
{
    internal byte ViewNumber => (byte)(ViewAndImpression >> 4);

    internal byte ImpressionType => (byte)(ViewAndImpression & 15);

    internal FingerprintView ToView(
        ImmutableArray<FingerprintMinutia> minutiae,
        ImmutableArray<FingerprintExtendedBlock> blocks
    ) => new(FingerPosition, ViewNumber, ImpressionType, Quality, minutiae, blocks);
}
