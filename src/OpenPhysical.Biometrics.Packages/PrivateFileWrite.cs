// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;

/// <summary>One explicitly requested private output; package callers pass protected envelope bytes only.</summary>
/// <param name="Path">Final destination supplied by the caller.</param>
/// <param name="Bytes">Owned immutable bytes to install atomically.</param>
public sealed record PrivateFileWrite(string Path, ImmutableArray<byte> Bytes);
