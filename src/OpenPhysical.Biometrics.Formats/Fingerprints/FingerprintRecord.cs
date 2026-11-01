// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;

/// <summary>Contains an immutable INCITS 378-2004 minutiae record with explicit image geometry.</summary>
/// <param name="ProductOwner">Registered extractor product owner.</param>
/// <param name="ProductType">Registered extractor product type.</param>
/// <param name="CaptureEquipment">Packed equipment compliance and identifier.</param>
/// <param name="Width">Capture width in pixels.</param>
/// <param name="Height">Capture height in pixels.</param>
/// <param name="XResolution">Horizontal resolution in pixels per centimetre.</param>
/// <param name="YResolution">Vertical resolution in pixels per centimetre.</param>
/// <param name="Views">Finger views in wire order. PIV policy is applied separately from this interchange model.</param>
public sealed record FingerprintRecord(
    ushort ProductOwner,
    ushort ProductType,
    ushort CaptureEquipment,
    ushort Width,
    ushort Height,
    ushort XResolution,
    ushort YResolution,
    ImmutableArray<FingerprintView> Views
);
