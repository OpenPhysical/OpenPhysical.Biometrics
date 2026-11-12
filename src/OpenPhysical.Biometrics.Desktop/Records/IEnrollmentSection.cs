// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>A registered biometric section belonging to the shared person session.</summary>
public interface IEnrollmentSection
{
    /// <summary>Gets the stable section identifier used on saved artifacts.</summary>
    string Id { get; }

    /// <summary>Gets the section's display title.</summary>
    string Title { get; }

    /// <summary>Gets whether all required captures have been accepted.</summary>
    bool Complete { get; }

    /// <summary>Gets the section's own presentation model.</summary>
    ObservableObject ViewModel { get; }

    /// <summary>Writes this section's actual artifacts for the shared person.</summary>
    /// <param name="person">The explicitly supplied local person label.</param>
    /// <param name="folder">The parent directory for the saved package.</param>
    /// <param name="cancellationToken">Cancellation for storage operations.</param>
    /// <returns>Saved artifact paths, or a precise failure.</returns>
    Task<Result<ImmutableArray<EnrollmentArtifact>, DesktopError>> WriteAsync(
        CapturePerson person,
        string folder,
        CancellationToken cancellationToken
    );

    /// <summary>Releases the section's accepted captures and starts a new plan.</summary>
    /// <returns>The reset outcome.</returns>
    Task<UnitResult<DesktopError>> ResetAsync();
}
