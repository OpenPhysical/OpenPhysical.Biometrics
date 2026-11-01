// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Faces;

using System.Collections.Immutable;

/// <summary>Contains all image blocks in one INCITS 385-2004 facial record.</summary>
/// <param name="Images">Image blocks in wire order.</param>
public sealed record FaceRecord(ImmutableArray<FaceImage> Images);
