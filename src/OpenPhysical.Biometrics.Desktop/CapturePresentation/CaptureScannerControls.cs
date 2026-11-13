// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

internal sealed class CaptureScannerControls
{
    private readonly CaptureCoordinator coordinator;
    private readonly CaptureViewModel view;

    internal CaptureScannerControls(
        CaptureCoordinator coordinator,
        CaptureViewModel view,
        Func<bool> stopping
    )
    {
        this.coordinator = coordinator;
        this.view = view;
        Exposure = new AsyncRelayCommand(ApplyAsync, () => !stopping());
        Reset = new AsyncRelayCommand(ResetAsync, () => !stopping());
    }

    internal IAsyncRelayCommand Exposure { get; }
    internal IAsyncRelayCommand Reset { get; }

    internal Task<UnitResult<DesktopError>> RestoreDefaultsAsync()
    {
        coordinator.SensorEvidence.Clear();
        view.Exposure = DesktopCaptureDefaults.Exposure;
        view.AutomaticExposure = true;
        return coordinator.SetExposureAsync(DesktopCaptureDefaults.Exposure, true);
    }

    private async Task ApplyAsync()
    {
        var result = await coordinator.SetExposureAsync((int)view.Exposure, view.AutomaticExposure);
        if (result.IsFailure)
            view.Report(result.Error);
    }

    private async Task ResetAsync()
    {
        var result = await coordinator.ResetScannerAsync(view.Captured.HasNoValue);
        if (result.IsFailure)
            view.Report(result.Error);
        else
            view.ScannerReady();
    }
}
