// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Describes a minutia in an INCITS 378-2004 record.</summary>
/// <param name="Type">Zero for other, one for a ridge ending, or two for a bifurcation.</param>
/// <param name="X">Horizontal pixel coordinate measured from the left image edge.</param>
/// <param name="Y">Vertical pixel coordinate measured from the top image edge.</param>
/// <param name="Angle">Counterclockwise angle from positive X in two-degree units, from zero through 179.</param>
/// <param name="Quality">Minutia quality from zero through 100; zero denotes unreported quality.</param>
public sealed record FingerprintMinutia(byte Type, ushort X, ushort Y, byte Angle, byte Quality);
