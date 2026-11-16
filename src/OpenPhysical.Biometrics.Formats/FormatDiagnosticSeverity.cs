// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

/// <summary>Separates rejected structures from explicitly selected interpretation warnings.</summary>
public enum FormatDiagnosticSeverity
{
    /// <summary>A structural failure prevents successful parsing.</summary>
    Error,

    /// <summary>An explicitly selected policy retains a field that needs interpretation before use.</summary>
    Warning,
}
