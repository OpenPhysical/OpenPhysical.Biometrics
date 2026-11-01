// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

using System.Collections.Immutable;

/// <summary>Contains structural inspection output independently of a renderer or logger.</summary>
/// <param name="Fields">Fields located before parsing completed or failed.</param>
/// <param name="Diagnostics">Structural failures with their input locations.</param>
public sealed record InspectionReport(
    ImmutableArray<InspectionField> Fields,
    ImmutableArray<FormatDiagnostic> Diagnostics
);
