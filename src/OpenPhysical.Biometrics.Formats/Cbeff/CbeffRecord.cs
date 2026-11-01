// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Cbeff;

using System.Collections.Immutable;

/// <summary>Contains an immutable PIV CBEFF header, biometric block, and detached security block.</summary>
/// <param name="Header">The exact header used for signed-content construction.</param>
/// <param name="BiometricData">Raw biometric interchange record bytes.</param>
/// <param name="Signature">Detached security-block bytes, or an initialized empty array.</param>
public sealed record CbeffRecord(
    CbeffHeader Header,
    ImmutableArray<byte> BiometricData,
    ImmutableArray<byte> Signature
);
