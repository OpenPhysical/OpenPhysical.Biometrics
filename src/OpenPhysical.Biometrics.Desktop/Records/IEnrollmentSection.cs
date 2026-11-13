// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>A person-owned capture section whose artifacts join one package transaction.</summary>
public interface IEnrollmentSection
{
    /// <summary>Gets the stable modality identifier.</summary>
    string Id { get; }

    /// <summary>Gets the operator-facing modality label.</summary>
    string Title { get; }

    /// <summary>Gets whether the required artifacts have been accepted.</summary>
    bool Complete { get; }

    /// <summary>Gets the number of required objects owned by this section.</summary>
    int RequiredCount => 1;

    /// <summary>Gets the number of explicitly accepted objects.</summary>
    int AcceptedCount => Complete ? 1 : 0;

    /// <summary>Gets whether capture or a section operation is running.</summary>
    bool IsBusy { get; }

    /// <summary>Gets whether an explicit adjustment draft is open.</summary>
    bool IsEditing { get; }

    /// <summary>Gets whether cancellation and reset can currently proceed.</summary>
    bool CanReset { get; }

    /// <summary>Gets the accepted-artifact progress.</summary>
    string Progress { get; }

    /// <summary>Gets the current operator instruction.</summary>
    string Instructions { get; }

    /// <summary>Gets the section presentation model.</summary>
    ObservableObject ViewModel { get; }

    /// <summary>Gets the explicit acceptance command.</summary>
    IAsyncRelayCommand AcceptCommand { get; }

    /// <summary>Gets operations drained before resetting or stopping this section.</summary>
    ImmutableArray<IAsyncRelayCommand> Commands { get; }

    /// <summary>Starts or suspends the acquisition source without discarding accepted data.</summary>
    /// <param name="active">Whether this section may acquire camera or scanner frames.</param>
    /// <returns>The activation or pause outcome.</returns>
    Task<UnitResult<DesktopError>> ActivateAsync(bool active);

    /// <summary>Builds accepted artifacts in memory without writing files.</summary>
    /// <returns>The accepted immutable artifacts or a precise readiness failure.</returns>
    Result<ImmutableArray<PackageArtifact>, DesktopError> PrepareArtifacts();

    /// <summary>Clears current and accepted person data.</summary>
    /// <returns>The cancellation and reset outcome.</returns>
    Task<UnitResult<DesktopError>> ResetAsync();

    /// <summary>Drains operations and releases capture resources.</summary>
    /// <returns>The drained shutdown outcome.</returns>
    Task<UnitResult<DesktopError>> StopAsync();
}
