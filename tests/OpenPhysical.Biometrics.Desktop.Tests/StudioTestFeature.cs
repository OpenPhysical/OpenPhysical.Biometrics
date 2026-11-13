// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class StudioTestFeature : IStudioWorkspaceFeature
{
    private readonly bool defer;
    private readonly TaskCompletionSource<UnitResult<DesktopError>> pending = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    internal StudioTestFeature(bool defer = false) => this.defer = defer;

    public string Title => "Signing";
    public Control Content { get; } = new StudioStagePanel("Signing", new TextBlock());
    internal TaskCompletionSource Activating { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool IsActive { get; private set; }
    internal bool Stopped { get; private set; }

    public async Task<UnitResult<DesktopError>> ActivateAsync()
    {
        IsActive = true;
        Activating.TrySetResult();
        return defer ? await pending.Task : UnitResult.Success<DesktopError>();
    }

    public Task<UnitResult<DesktopError>> PauseAsync()
    {
        IsActive = false;
        if (Activating.Task.IsCompleted)
            pending.TrySetResult(UnitResult.Success<DesktopError>());
        return Task.FromResult(UnitResult.Success<DesktopError>());
    }

    public Task<UnitResult<DesktopError>> StopAsync()
    {
        Stopped = true;
        return PauseAsync();
    }
}
