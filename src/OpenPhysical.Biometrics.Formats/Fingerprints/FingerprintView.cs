// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;

/// <summary>Contains one finger impression and its associated extended blocks.</summary>
/// <param name="FingerPosition">ANSI finger position code.</param>
/// <param name="ViewNumber">View number from zero through 15.</param>
/// <param name="ImpressionType">Four-bit impression code, such as zero for live plain capture.</param>
/// <param name="Quality">Finger quality from zero through 100, or unavailable status 254 or 255.</param>
/// <param name="Minutiae">Minutiae in the original record order.</param>
/// <param name="ExtendedBlocks">Extended blocks in the original record order.</param>
public sealed record FingerprintView(
    byte FingerPosition,
    byte ViewNumber,
    byte ImpressionType,
    byte Quality,
    ImmutableArray<FingerprintMinutia> Minutiae,
    ImmutableArray<FingerprintExtendedBlock> ExtendedBlocks
);
