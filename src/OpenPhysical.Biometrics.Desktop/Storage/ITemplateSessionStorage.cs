// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Saves a person's selected captures and unsigned template as one local package.</summary>
public interface ITemplateSessionStorage
{
    /// <summary>Saves with an explicit decision about additional diagnostic image planes.</summary>
    /// <param name="record">The unsigned record.</param>
    /// <param name="captures">Accepted captures in record order.</param>
    /// <param name="person">The supplied local person label.</param>
    /// <param name="folder">Private package parent directory.</param>
    /// <param name="options">Whether to include optional diagnostic images.</param>
    /// <param name="cancellationToken">Cancellation for file operations.</param>
    /// <returns>The saved path or a precise failure.</returns>
    Task<Result<string, DesktopError>> SaveSessionAsync(
        FingerprintRecord record,
        ImmutableArray<CapturedFingerprint> captures,
        CapturePerson person,
        string folder,
        TemplateSaveOptions options,
        CancellationToken cancellationToken
    ) =>
        options.IncludeDiagnostics
            ? Task.FromResult(
                Result.Failure<string, DesktopError>(
                    new(
                        "Storage.DiagnosticsUnsupported",
                        "This storage provider does not support diagnostic image export."
                    )
                )
            )
            : SaveSessionAsync(record, captures, person, folder, cancellationToken);

    /// <summary>Associates the explicitly supplied person label with the selected finger views.</summary>
    /// <param name="record">The unsigned record displayed for export.</param>
    /// <param name="captures">The accepted captures in record order.</param>
    /// <param name="person">The user-provided local name or alias and optional identifier.</param>
    /// <param name="folder">The parent directory for the private package.</param>
    /// <param name="cancellationToken">Cancellation for storage operations.</param>
    /// <returns>The saved template path or a precise failure.</returns>
    Task<Result<string, DesktopError>> SaveSessionAsync(
        FingerprintRecord record,
        ImmutableArray<CapturedFingerprint> captures,
        CapturePerson person,
        string folder,
        CancellationToken cancellationToken
    );
}
