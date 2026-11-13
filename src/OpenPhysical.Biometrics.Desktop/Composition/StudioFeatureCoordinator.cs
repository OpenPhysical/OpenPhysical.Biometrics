// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Serializes shared navigation and drains every task on lock or shutdown.</summary>
public sealed class StudioFeatureCoordinator
{
    private readonly WorkspaceViewModel workspace;
    private readonly SemaphoreSlim transition = new(1, 1);
    private long generation;
    private bool stopping;
    private bool active;
    private int selected;

    /// <summary>Connects capture and comparison to application-owned feature tasks.</summary>
    /// <param name="workspace">The existing shared person and biometric sources.</param>
    /// <param name="features">Trusted task registrations in navigation order.</param>
    public StudioFeatureCoordinator(
        WorkspaceViewModel workspace,
        IEnumerable<IStudioWorkspaceFeature> features
    )
    {
        this.workspace = workspace;
        Features = [.. features];
        ClearPersonCommand = new AsyncRelayCommand(
            ClearAsync,
            () => !stopping && workspace.ClearPersonCommand.CanExecute(default)
        );
        workspace.ClearPersonCommand.CanExecuteChanged += (_, _) =>
            ClearPersonCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Gets application tasks following Capture and Compare.</summary>
    public ImmutableArray<IStudioWorkspaceFeature> Features { get; }

    /// <summary>Gets clearing that cancels feature work before releasing the accepted person.</summary>
    public IAsyncRelayCommand ClearPersonCommand { get; }

    /// <summary>Pauses every source before activating the selected navigation task.</summary>
    /// <param name="index">Capture is zero, Compare is one, then registered features.</param>
    /// <returns>The complete source transition outcome.</returns>
    public async Task<UnitResult<DesktopError>> SelectAsync(int index)
    {
        if (index < 0 || index >= Features.Length + 2)
            return new DesktopError("Workspace.Selection", "Select an available workspace.");
        var expected = Interlocked.Increment(ref generation);
        var interrupted = await PauseCoreAsync();
        await transition.WaitAsync();
        try
        {
            if (stopping)
                return Closed();
            if (expected != Volatile.Read(ref generation))
                return UnitResult.Success<DesktopError>();
            var paused = await PauseCoreAsync();
            if (interrupted.IsFailure || paused.IsFailure)
                return interrupted.IsFailure ? interrupted : paused;
            if (stopping || expected != Volatile.Read(ref generation))
                return UnitResult.Success<DesktopError>();
            selected = index;
            active = true;
            return index < 2
                ? await workspace.ActivateAsync(index == 0)
                : await Features[index - 2].ActivateAsync();
        }
        finally
        {
            transition.Release();
        }
    }

    /// <summary>Cancels pending activation and drains all sources and feature work.</summary>
    /// <returns>The pause outcome without reactivating any task.</returns>
    public Task<UnitResult<DesktopError>> PauseAsync()
    {
        active = false;
        Interlocked.Increment(ref generation);
        return DrainPauseAsync();
    }

    private async Task<UnitResult<DesktopError>> DrainPauseAsync()
    {
        var interrupted = await PauseCoreAsync();
        await transition.WaitAsync();
        try
        {
            var paused = await PauseCoreAsync();
            return interrupted.IsFailure ? interrupted : paused;
        }
        finally
        {
            transition.Release();
        }
    }

    /// <summary>Stops feature work and both biometric sources before host disposal.</summary>
    /// <returns>The first failure after all tasks have drained.</returns>
    public async Task<UnitResult<DesktopError>> StopAsync()
    {
        stopping = true;
        ClearPersonCommand.Cancel();
        var paused = await PauseAsync();
        var stopped = await Task.WhenAll(
            Features.Select(feature => feature.StopAsync()).Append(workspace.StopSourcesAsync())
        );
        var drained = await DesktopCommandTasks.DrainAsync(ClearPersonCommand);
        return paused.IsFailure ? paused
            : drained.IsFailure ? drained
            : FirstFailure(stopped);
    }

    private async Task ClearAsync()
    {
        var resume = active && selected < 2;
        active = false;
        var expected = Interlocked.Increment(ref generation);
        var paused = await DrainPauseAsync();
        if (paused.IsFailure)
        {
            workspace.Export.Report(paused.Error);
            return;
        }
        await workspace.ClearPersonCommand.ExecuteAsync(default);
        if (resume && !stopping && expected == Volatile.Read(ref generation))
        {
            var resumed = await SelectAsync(selected);
            if (resumed.IsFailure)
                workspace.Export.Report(resumed.Error);
        }
    }

    private async Task<UnitResult<DesktopError>> PauseCoreAsync() =>
        FirstFailure(
            await Task.WhenAll(
                Features.Select(feature => feature.PauseAsync()).Append(workspace.PauseAsync())
            )
        );

    private static UnitResult<DesktopError> FirstFailure(
        IEnumerable<UnitResult<DesktopError>> outcomes
    )
    {
        foreach (var outcome in outcomes)
            if (outcome.IsFailure)
                return outcome;
        return UnitResult.Success<DesktopError>();
    }

    private static UnitResult<DesktopError> Closed() =>
        new DesktopError("Workspace.Closed", "The application workspace has ended.");
}
