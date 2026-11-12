// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Persists and reopens unsigned templates and their explicitly paired source images.</summary>
public interface ITemplateStorage
{
    /// <summary>Atomically saves a template and its captured source evidence in a private directory.</summary>
    /// <param name="record">The exact record displayed for export.</param>
    /// <param name="captures">Captured images corresponding to the record's finger views.</param>
    /// <param name="folder">Parent directory for the new capture package.</param>
    /// <param name="cancellationToken">Cancellation for file operations.</param>
    /// <returns>The saved FMR path, or a precise validation or storage failure.</returns>
    Task<Result<string, DesktopError>> SaveAsync(
        FingerprintRecord record,
        ImmutableArray<CapturedFingerprint> captures,
        string folder,
        CancellationToken cancellationToken
    );

    /// <summary>Reopens an FMR or raw CBEFF and verifies any adjacent image manifest.</summary>
    /// <param name="path">Template file selected for inspection.</param>
    /// <param name="cancellationToken">Cancellation for file operations.</param>
    /// <returns>The decoded record and verified source images, or a precise failure.</returns>
    Task<Result<TemplateDocument, DesktopError>> LoadAsync(
        string path,
        CancellationToken cancellationToken
    );
}
