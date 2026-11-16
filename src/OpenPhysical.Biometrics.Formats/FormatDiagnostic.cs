// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

/// <summary>Describes a format finding by diagnostic code and byte location.</summary>
/// <param name="Code">Stable, machine-readable error identifier.</param>
/// <param name="Message">Explanation of the failed structural check.</param>
/// <param name="Offset">Zero-based byte offset in the supplied buffer.</param>
/// <param name="Length">Size of the affected field or available fragment.</param>
/// <param name="Severity">Error by default; explicit interpretation policies may report warnings.</param>
public sealed record FormatDiagnostic(
    string Code,
    string Message,
    int Offset,
    int Length,
    FormatDiagnosticSeverity Severity = FormatDiagnosticSeverity.Error
);
