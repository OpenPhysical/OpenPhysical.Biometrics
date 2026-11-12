// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class DesktopCommandTasks
{
    internal static async Task<UnitResult<DesktopError>> DrainAsync(
        params IAsyncRelayCommand[] commands
    )
    {
        var running = commands.Select(command => command.ExecutionTask).OfType<Task>().ToArray();
        var drained = await DesktopEdge.TryAsync(
            async () =>
            {
                await Task.WhenAll(running);
                return true;
            },
            "OperationShutdown",
            "An active operation could not finish."
        );
        return drained.IsSuccess
            ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(drained.Error);
    }
}
