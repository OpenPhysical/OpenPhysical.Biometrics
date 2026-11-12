// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed class CaptureScannerReset(
    CaptureDeviceConnection connection,
    CapturePreviewState state,
    CapturePreviewPump pump,
    CancellationToken shutdown
)
{
    internal async Task<UnitResult<DesktopError>> RunAsync(
        Func<Task<UnitResult<DesktopError>>> pause,
        Func<Task<UnitResult<DesktopError>>> resume,
        bool shouldResume
    )
    {
        var paused = await pause();
        if (paused.IsFailure)
            return paused;
        state.ForgetDisplayed();
        var closed = await connection.CloseAsync();
        if (closed.IsFailure)
            return closed;
        pump.ResetRecovery();
        var opened = await connection.EnsureOpenAsync(state.Exposure.Settings, shutdown);
        if (opened.IsFailure)
            return opened;
        return shouldResume ? await resume() : UnitResult.Success<DesktopError>();
    }
}
