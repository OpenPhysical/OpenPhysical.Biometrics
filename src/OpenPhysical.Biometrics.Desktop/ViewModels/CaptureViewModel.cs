// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Exposes the operator capture, retry, scanner reset and shutdown workflow.</summary>
public sealed class CaptureViewModel : CaptureAdjustedViewModel
{
    private readonly CaptureCoordinator coordinator;
    private readonly CaptureWork work;
    private readonly CaptureViewLifecycle lifecycle;
    private readonly CaptureScannerControls scanner;
    private readonly CaptureAttemptSelection selection;

    /// <summary>Creates the operator workflow and attaches its actual UI dispatcher.</summary>
    /// <param name="coordinator">Scanner lifecycle.</param>
    /// <param name="processor">Image assessment and extraction service.</param>
    /// <param name="dispatcher">Avalonia preview presentation adapter.</param>
    /// <param name="clock">Monotonic presentation clock.</param>
    public CaptureViewModel(
        CaptureCoordinator coordinator,
        ICaptureProcessor processor,
        DesktopPreviewDispatcher dispatcher,
        TimeProvider clock
    )
        : base(clock, coordinator, processor)
    {
        this.coordinator = coordinator;
        work = new CaptureWork(coordinator, processor);
        lifecycle = new CaptureViewLifecycle(coordinator);
        scanner = new CaptureScannerControls(coordinator, this, () => lifecycle.Stopping);
        selection = new CaptureAttemptSelection(coordinator, automation);
        dispatcher.Attach(this);
        CaptureCommand = new AsyncRelayCommand(
            CaptureAsync,
            () =>
                !lifecycle.Stopping
                && coordinator.HasDisplayedFrame
                && coordinator.IsLive
                && HasUsablePreview
                && HasCurrentProcessingPreview
                && (!automation.RequiresRemoval || automation.HasAutomaticSelection)
                && !busy
        );
        RetakeCommand = new AsyncRelayCommand(() => ResetAsync(true), () => !lifecycle.Stopping);
        ResetCaptureCommand = new AsyncRelayCommand(
            () => ResetAsync(false),
            () => !lifecycle.Stopping
        );
    }

    /// <inheritdoc/>
    private protected override bool IsStopping => lifecycle.Stopping;

    /// <inheritdoc/>
    public override IAsyncRelayCommand CaptureCommand { get; }

    /// <summary>Gets the next-presentation command, requiring finger removal.</summary>
    public IAsyncRelayCommand RetakeCommand { get; }

    /// <summary>Gets the reset command that clears the current capture and rearms immediately.</summary>
    public IAsyncRelayCommand ResetCaptureCommand { get; }

    /// <summary>Gets the command applying operator exposure controls.</summary>
    public IAsyncRelayCommand ExposureCommand => scanner.Exposure;

    /// <summary>Gets the command reconnecting the scanner after an acquisition fault.</summary>
    public IAsyncRelayCommand ResetScannerCommand => scanner.Reset;

    /// <summary>Gets whether a preview, frozen capture or processing operation is active.</summary>
    public bool HasActiveAttempt => Captured.HasValue || LiveFrame.HasValue || busy;

    /// <summary>Validates the continuous-presentation capture delay.</summary>
    /// <param name="value">Delay from zero through ten seconds.</param>
    /// <returns>Configuration outcome.</returns>
    public UnitResult<DesktopError> ConfigureAutoCaptureDelay(TimeSpan value)
    {
        var result = automation.ConfigureDelay(value);
        if (result.IsSuccess)
            coordinator.AutoCaptureDelay = value;
        return result;
    }

    /// <summary>Starts acquisition using the displayed exposure controls.</summary>
    /// <param name="token">Application shutdown cancellation.</param>
    /// <returns>Start outcome.</returns>
    public Task<UnitResult<DesktopError>> StartAsync(CancellationToken token) =>
        lifecycle.StartAsync((int)Exposure, AutomaticExposure, token);

    /// <summary>Clears the current attempt and restores acquisition defaults while preserving the selected finger.</summary>
    /// <returns>Reset outcome.</returns>
    public async Task<UnitResult<DesktopError>> ResetDefaultsAsync()
    {
        coordinator.SensorEvidence.Clear();
        var configured = await scanner.RestoreDefaultsAsync();
        return configured.IsFailure ? configured : await ResetAsync(false);
    }

    /// <summary>Cancels and drains processing, stops acquisition and releases displayed scenes.</summary>
    /// <returns>Shutdown outcome.</returns>
    public async Task<UnitResult<DesktopError>> StopAsync()
    {
        CancelAdjustments();
        attempt++;
        automation.Reset(false);
        var stopped = await lifecycle.StopAsync(
            CaptureCommand,
            RetakeCommand,
            ResetCaptureCommand,
            ExposureCommand,
            ResetScannerCommand
        );
        var adjusted = await DrainAdjustmentsAsync();
        ClearPreview();
        Captured = Maybe<CapturedFingerprint>.None;
        return stopped.IsFailure ? stopped : adjusted;
    }

    private async Task CaptureAsync(CancellationToken token)
    {
        CancelAdjustments();
        var generation = ++attempt;
        var imagePolicy = coordinator.Images.Snapshot();
        var finger = SelectedFinger.Position;
        busy = true;
        CaptureGuidance = "Capturing";
        var snapshot = selection.Snapshot(imagePolicy);
        if (snapshot.IsFailure)
        {
            busy = false;
            Report(snapshot.Error);
            return;
        }
        var request = snapshot.Value.Request;
        originalAcquisition = request.OriginalAcquisitionPolicy;
        originalInput = request.Processing.Input;
        CaptureCommand.NotifyCanExecuteChanged();
        var result = await work.RunAsync(
            snapshot.Value.Frame,
            finger,
            frame => ShowFrozenPreview(frame, imagePolicy, token),
            request,
            Maybe.From<Func<FingerprintPreprocessingArtifact, UnitResult<DesktopError>>>(artifact =>
                generation == attempt && !lifecycle.Stopping
                    ? PresentProcessing(artifact)
                    : UnitResult.Failure(new DesktopError("Cancelled", "Capture was reset."))
            ),
            token
        );
        if (lifecycle.Stopping || generation != attempt)
            return;
        busy = false;
        CompleteProcessing(result);
    }

    private async Task<UnitResult<DesktopError>> ResetAsync(bool requireRemoval)
    {
        CaptureCommand.Cancel();
        CancelAdjustments();
        attempt++;
        Status = requireRemoval ? "Waiting for finger removal." : "Waiting for a fingerprint.";
        Captured = Maybe<CapturedFingerprint>.None;
        Problem = string.Empty;
        ClearPreview();
        automation.Reset(requireRemoval);
        CaptureGuidance = requireRemoval ? CapturePreviewGuidance.Removal : "Place your finger";
        busy = false;
        var result = await work.ResumeAsync(CaptureCommand, () => lifecycle.Stopping);
        if (result.IsFailure)
            Report(result.Error);
        CaptureCommand.NotifyCanExecuteChanged();
        return result;
    }

    /// <summary>Pauses acquisition and displays an immutable retained capture for frozen review.</summary>
    /// <param name="accepted">Retained source selected for review.</param>
    /// <returns>Presentation outcome.</returns>
    public Task<UnitResult<DesktopError>> ReviewAcceptedAsync(CapturedFingerprint accepted)
    {
        CancelAdjustments();
        reviewingAccepted = true;
        attempt++;
        automation.Reset(true);
        busy = false;
        Problem = string.Empty;
        return work.ReviewAsync(
            CaptureCommand,
            () =>
                lifecycle.Stopping ? UnitResult.Success<DesktopError>() : CompleteCapture(accepted)
        );
    }

    internal UnitResult<DesktopError> SourceError(DesktopError error)
    {
        if (lifecycle.Stopping || busy || Captured.HasValue)
            return UnitResult.Success<DesktopError>();
        attempt++;
        automation.Reset(false);
        coordinator.InvalidatePreview();
        ClearPreview();
        ScannerResetRequired = !coordinator.IsLive;
        return Report(error);
    }
}
