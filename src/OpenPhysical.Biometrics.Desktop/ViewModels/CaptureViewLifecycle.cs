// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class CaptureViewLifecycle(CaptureCoordinator coordinator)
{
    internal bool Stopping { get; private set; }

    internal async Task<UnitResult<DesktopError>> StartAsync(
        int exposure,
        bool automatic,
        CancellationToken token
    )
    {
        if (Stopping)
            return new DesktopError("Capture.Closed", "Capture has stopped.");
        var configured = await coordinator.SetExposureAsync(exposure, automatic);
        return configured.IsFailure ? configured : await coordinator.StartAsync(token);
    }

    internal async Task<UnitResult<DesktopError>> StopAsync(params IAsyncRelayCommand[] commands)
    {
        Stopping = true;
        foreach (var command in commands)
            command.NotifyCanExecuteChanged();
        var stopped = await coordinator.StopAsync();
        var drained = await DesktopCommandTasks.DrainAsync(commands);
        return stopped.IsFailure ? stopped : drained;
    }
}
