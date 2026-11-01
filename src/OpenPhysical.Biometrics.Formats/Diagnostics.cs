using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Formats;

/// <summary>Describes a format error by diagnostic code and byte location.</summary>
/// <param name="Code">Stable, machine-readable error identifier.</param>
/// <param name="Message">Explanation of the failed structural check.</param>
/// <param name="Offset">Zero-based byte offset in the supplied buffer.</param>
/// <param name="Length">Size of the affected field or available fragment.</param>
public sealed record FormatDiagnostic(string Code, string Message, int Offset, int Length);

/// <summary>Controls whether inspection reports include field contents.</summary>
/// <param name="IncludeSensitiveValues">Enables field values, including biometric and identity bytes, for explicitly requested inspection.</param>
public sealed record InspectionOptions(bool IncludeSensitiveValues = false);

/// <summary>Locates a binary field in its original input.</summary>
/// <param name="Name">Stable field path.</param>
/// <param name="Offset">Zero-based byte offset.</param>
/// <param name="Length">Field size in bytes.</param>
/// <param name="Value">Hexadecimal field contents when sensitive inspection is enabled; otherwise null.</param>
public sealed record InspectionField(string Name, int Offset, int Length, string? Value);

/// <summary>Contains structural inspection output independently of a renderer or logger.</summary>
/// <param name="Fields">Fields located before parsing completed or failed.</param>
/// <param name="Diagnostics">Structural failures with their input locations.</param>
public sealed record InspectionReport(
    ImmutableArray<InspectionField> Fields,
    ImmutableArray<FormatDiagnostic> Diagnostics
);

/// <summary>Returns a parsed immutable value or located structural diagnostics.</summary>
/// <typeparam name="T">Parsed record type.</typeparam>
public sealed class ParseResult<T>
    where T : class
{
    internal ParseResult(
        T? value,
        IEnumerable<InspectionField> fields,
        IEnumerable<FormatDiagnostic> diagnostics
    )
    {
        Value = value;
        Inspection = new InspectionReport(
            fields.ToImmutableArray(),
            diagnostics.ToImmutableArray()
        );
    }

    /// <summary>Gets whether the complete buffer was parsed successfully.</summary>
    public bool IsSuccess => Value is not null && Diagnostics.IsEmpty;

    /// <summary>Gets the parsed value, or null when parsing failed.</summary>
    public T? Value { get; }

    /// <summary>Gets structural errors. Messages contain no field contents.</summary>
    public ImmutableArray<FormatDiagnostic> Diagnostics => Inspection.Diagnostics;

    /// <summary>Gets field locations and explicitly requested field values.</summary>
    public InspectionReport Inspection { get; }
}
