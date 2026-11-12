// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed class CaptureWork(CaptureCoordinator coordinator, ICaptureProcessor processor)
{
    internal async Task<UnitResult<DesktopError>> ResumeAsync(
        IAsyncRelayCommand capture,
        Func<bool> stopping
    )
    {
        var drained = await DesktopCommandTasks.DrainAsync(capture);
        return stopping() || drained.IsFailure
            ? drained
            : await coordinator.ResumeAsync(CancellationToken.None);
    }

    internal async Task<UnitResult<DesktopError>> ReviewAsync(
        IAsyncRelayCommand capture,
        Func<UnitResult<DesktopError>> show
    )
    {
        var paused = await coordinator.PauseAsync();
        if (paused.IsFailure)
            return paused;
        var drained = await DesktopCommandTasks.DrainAsync(capture);
        return drained.IsFailure ? drained : show();
    }

    internal async Task<Result<CapturedFingerprint, DesktopError>> RunAsync(
        CaptureFrame frame,
        FingerPosition finger,
        Func<CaptureFrame, UnitResult<DesktopError>> present,
        CaptureWorkRequest request,
        Maybe<Func<FingerprintPreprocessingArtifact, UnitResult<DesktopError>>> showProcessed =
            default,
        CancellationToken token = default
    )
    {
        var frozen = await coordinator.FreezeAsync(Maybe.From(frame), present);
        if (frozen.IsFailure)
            return frozen.Error;
        coordinator.InvalidatePreview();
        return await new CaptureQualityWork(processor).RunAsync(
            frozen.Value,
            finger,
            request,
            showProcessed,
            token
        );
    }
}
