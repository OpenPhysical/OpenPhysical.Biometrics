// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

using System.Collections.Immutable;

/// <summary>Describes a failed parse or write with located diagnostics.</summary>
/// <param name="Code">Stable, machine-readable identifier of the first failure.</param>
/// <param name="Message">Explanation of the first failure; it contains no field contents.</param>
/// <param name="Inspection">Fields located before the failure and every diagnostic.</param>
public sealed record FormatError(string Code, string Message, InspectionReport Inspection)
{
    /// <summary>Gets the structural errors, each with its input location.</summary>
    public ImmutableArray<FormatDiagnostic> Diagnostics => Inspection.Diagnostics;

    /// <summary>Creates an error that does not refer to a position in parsed input.</summary>
    /// <param name="code">Stable, machine-readable identifier.</param>
    /// <param name="message">Explanation of the failed check.</param>
    /// <returns>An error whose diagnostic is located at the start of the record.</returns>
    public static FormatError Invalid(string code, string message) =>
        new(code, message, new InspectionReport([], [new FormatDiagnostic(code, message, 0, 0)]));
}
