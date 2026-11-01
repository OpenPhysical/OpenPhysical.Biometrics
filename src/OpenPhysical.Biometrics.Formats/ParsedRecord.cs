// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

/// <summary>Contains a completely parsed immutable record and the fields located while parsing it.</summary>
/// <typeparam name="T">The parsed record type.</typeparam>
/// <param name="Record">The parsed record.</param>
/// <param name="Inspection">Field locations and explicitly requested field values.</param>
public sealed record ParsedRecord<T>(T Record, InspectionReport Inspection);
