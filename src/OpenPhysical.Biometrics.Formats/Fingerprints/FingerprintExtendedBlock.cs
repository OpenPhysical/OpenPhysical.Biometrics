// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;

/// <summary>Preserves the original bytes of an extended block, including unrecognized vendor data.</summary>
/// <param name="Type">Two-byte block type identifier.</param>
/// <param name="Data">Payload bytes following the four-byte type-and-length header.</param>
public sealed record FingerprintExtendedBlock(ushort Type, ImmutableArray<byte> Data);
