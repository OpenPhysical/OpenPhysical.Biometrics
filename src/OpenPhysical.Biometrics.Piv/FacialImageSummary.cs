// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

/// <summary>Describes facial image dimensions and encoding metadata.</summary>
/// <param name="Width">Encoded width in pixels.</param>
/// <param name="Height">Encoded height in pixels.</param>
/// <param name="ImageDataType">The encoded image compression type.</param>
public sealed record FacialImageSummary(ushort Width, ushort Height, byte ImageDataType);
