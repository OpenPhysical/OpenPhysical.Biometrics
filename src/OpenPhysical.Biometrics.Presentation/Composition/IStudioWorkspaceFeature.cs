// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>A trusted application task sharing the person, privacy and source lifetime.</summary>
public interface IStudioWorkspaceFeature
{
    /// <summary>Gets the task's navigation label.</summary>
    string Title { get; }

    /// <summary>Gets the task's application-owned control.</summary>
    Control Content { get; }

    /// <summary>Activates the task after every biometric source has paused.</summary>
    /// <returns>The activation outcome.</returns>
    Task<UnitResult<DesktopError>> ActivateAsync();

    /// <summary>Cancels and drains active work, including an incomplete activation.</summary>
    /// <returns>The cancellation and drain outcome.</returns>
    Task<UnitResult<DesktopError>> PauseAsync();

    /// <summary>Permanently stops and drains the task before its host is disposed.</summary>
    /// <returns>The shutdown outcome.</returns>
    Task<UnitResult<DesktopError>> StopAsync();
}
