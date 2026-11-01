// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Faces;

/// <summary>Describes one facial feature point.</summary>
/// <param name="Type">Feature-point encoding type.</param>
/// <param name="Identifier">Feature point identifier within that encoding.</param>
/// <param name="X">Horizontal image coordinate.</param>
/// <param name="Y">Vertical image coordinate.</param>
public sealed record FaceFeaturePoint(byte Type, byte Identifier, ushort X, ushort Y);
