// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Coordinates visible biometric sources and the shared person workspace.</summary>
public sealed class WorkspaceViewModel
{
    private readonly WorkspaceSourceLifecycle sources;

    /// <summary>Connects capture, person, comparison and persistent operator preferences.</summary>
    /// <param name="capture">Fingerprint source and prepared images.</param>
    /// <param name="export">Shared person and encrypted packaging.</param>
    /// <param name="inspection">Reference comparison.</param>
    /// <param name="settings">Operator preferences.</param>
    public WorkspaceViewModel(
        CaptureViewModel capture,
        ExportViewModel export,
        InspectionViewModel inspection,
        SettingsViewModel settings
    )
    {
        Capture = capture;
        Export = export;
        Inspection = inspection;
        Settings = settings;
        sources = new(capture, export.Enrollment);
        ClearPersonCommand = new AsyncRelayCommand(
            ClearAsync,
            () => export.ClearPersonCommand.CanExecute(default)
        );
        export.ClearPersonCommand.CanExecuteChanged += (_, _) =>
            ClearPersonCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Activates enrollment or fingerprint comparison and stops the other source.</summary>
    /// <param name="captureWorkspace">Whether enrollment rather than Compare is visible.</param>
    /// <returns>The source transition outcome.</returns>
    public Task<UnitResult<DesktopError>> ActivateAsync(bool captureWorkspace) =>
        sources.ActivateAsync(captureWorkspace);

    /// <summary>Pauses all sources, including comparison capture, for application lock.</summary>
    /// <returns>The acquisition drain outcome.</returns>
    public Task<UnitResult<DesktopError>> PauseAsync() => sources.PauseAsync();

    /// <summary>Stops sources and drains clearing before the person session is released.</summary>
    /// <returns>The source shutdown outcome.</returns>
    public async Task<UnitResult<DesktopError>> StopSourcesAsync()
    {
        ClearPersonCommand.Cancel();
        var stopped = await sources.StopAsync();
        var drained = await DesktopCommandTasks.DrainAsync(ClearPersonCommand);
        return stopped.IsFailure ? stopped : drained;
    }

    /// <summary>Gets clearing that resumes only the still-visible, unlocked workspace.</summary>
    public IAsyncRelayCommand ClearPersonCommand { get; }

    /// <summary>Gets fingerprint capture controls and review state.</summary>
    public CaptureViewModel Capture { get; }

    /// <summary>Gets the shared person and package workflow.</summary>
    public ExportViewModel Export { get; }

    /// <summary>Gets reference inspection and comparison.</summary>
    public InspectionViewModel Inspection { get; }

    /// <summary>Gets persistent operator preferences.</summary>
    public SettingsViewModel Settings { get; }

    private async Task ClearAsync()
    {
        var cleared = await sources.ClearAsync(() =>
            Export.ClearPersonCommand.ExecuteAsync(default)
        );
        if (cleared.IsFailure)
            Export.Report(cleared.Error);
    }
}
