// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Capture;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Coordinates scanner connection, acknowledged previews and bounded acquisition shutdown.</summary>
public sealed class CaptureCoordinator : IAsyncDisposable
{
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly CapturePreviewState state;
    private readonly CaptureDeviceConnection connection;
    private readonly CapturePreviewPump pump;
    private readonly CaptureScannerReset scannerReset;
    private readonly CancellationTokenSource shutdown = new();
    internal CaptureImagePreparation Images { get; } = new();
    internal CaptureSensorEvidence SensorEvidence { get; }
    internal CapturePolicySettings Policy => SensorEvidence.Policy;
    internal TimeSpan AutoCaptureDelay { get; set; } = TimeSpan.FromSeconds(1);

    internal UnitResult<DesktopError> ConfigurePolicy(CapturePolicySettings policy) =>
        SensorEvidence.Configure(policy).Bind(() => state.ConfigurePolicy(policy.Presentation));

    private readonly CaptureRunLifetime run;
    private bool stopped;

    /// <summary>Creates a scanner lifecycle with an explicit preview presentation boundary.</summary>
    /// <param name="registry">Registered capture drivers.</param>
    /// <param name="dispatcher">Acknowledges actual UI presentations.</param>
    /// <param name="clock">Monotonic lifecycle clock.</param>
    /// <param name="logger">Status logger without captured pixels.</param>
    public CaptureCoordinator(
        CaptureDriverRegistry registry,
        IPreviewDispatcher dispatcher,
        TimeProvider clock,
        ILogger<CaptureCoordinator> logger
    )
    {
        state = new CapturePreviewState(clock);
        connection = new CaptureDeviceConnection(registry, clock);
        SensorEvidence = new CaptureSensorEvidence(clock);
        pump = new(state, connection, dispatcher, clock, logger, Images, SensorEvidence);
        scannerReset = new CaptureScannerReset(connection, state, pump, shutdown.Token);
        run = new CaptureRunLifetime(pump, shutdown.Token);
    }

    /// <summary>Gets whether acquisition is publishing new previews.</summary>
    public bool IsLive => state.IsLive;

    /// <summary>Gets whether a complete preview has been acknowledged.</summary>
    public bool HasDisplayedFrame => state.HasDisplayedFrame;

    internal Result<CaptureSelectionSnapshot, DesktopError> SnapshotWork(
        Maybe<CaptureFrame> selected,
        ImmutableArray<FingerCaptureCandidate> candidates
    ) => state.SnapshotWork(selected, candidates);

    internal CaptureProcessingInput LiveInput(CaptureFrame frame) => state.LiveInput(frame);

    /// <summary>Starts acquisition or leaves the current live acquisition running.</summary>
    /// <param name="appLifetime">Application shutdown cancellation.</param>
    /// <returns>Start outcome; connection status arrives through the dispatcher.</returns>
    public async Task<UnitResult<DesktopError>> StartAsync(CancellationToken appLifetime)
    {
        await operations.WaitAsync().ConfigureAwait(false);
        try
        {
            if (stopped)
                return new DesktopError("Capture.Closed", "The capture utility has stopped.");
            if (run.IsRunning)
                return UnitResult.Success<DesktopError>();
            _ = run.Start(appLifetime, state.Begin);
            return UnitResult.Success<DesktopError>();
        }
        finally
        {
            operations.Release();
        }
    }

    /// <summary>Freezes the latest acknowledged image and drains the outstanding acquisition.</summary>
    /// <returns>That exact displayed frame, or a missing-preview failure.</returns>
    public Task<Result<CaptureFrame, DesktopError>> FreezeAsync() =>
        AwaitFrozenAsync(state.Freeze());

    internal Task<Result<CaptureFrame, DesktopError>> FreezeAsync(
        Maybe<CaptureFrame> selected,
        Func<CaptureFrame, UnitResult<DesktopError>> present
    )
    {
        var frozen = selected.Match(state.Freeze, state.Freeze);
        if (frozen.IsSuccess)
        {
            var shown = present(frozen.Value);
            if (shown.IsFailure)
                frozen = shown.Error;
        }
        return AwaitFrozenAsync(frozen);
    }

    private async Task<Result<CaptureFrame, DesktopError>> AwaitFrozenAsync(
        Result<CaptureFrame, DesktopError> frozen
    )
    {
        if (frozen.IsFailure)
            return frozen;
        await operations.WaitAsync().ConfigureAwait(false);
        try
        {
            await run.WaitAsync().ConfigureAwait(false);
            return frozen;
        }
        finally
        {
            operations.Release();
        }
    }

    internal bool InvalidatePreview() => state.ForgetDisplayed();

    /// <summary>Closes the scanner and clears fault recovery before opening a fresh session.</summary>
    /// <param name="resume">Resume live acquisition after reconnecting.</param>
    /// <returns>Reconnect outcome.</returns>
    public Task<UnitResult<DesktopError>> ResetScannerAsync(bool resume)
    {
        SensorEvidence.Clear();
        return scannerReset.RunAsync(PauseAsync, () => ResumeAsync(CancellationToken.None), resume);
    }

    /// <summary>Stops preview publication and drains the outstanding acquisition.</summary>
    /// <returns>Pause outcome.</returns>
    public async Task<UnitResult<DesktopError>> PauseAsync()
    {
        state.Pause();
        await operations.WaitAsync().ConfigureAwait(false);
        try
        {
            await run.WaitAsync().ConfigureAwait(false);
            return UnitResult.Success<DesktopError>();
        }
        finally
        {
            operations.Release();
        }
    }

    /// <summary>Drains previous acquisition and resumes live previews.</summary>
    /// <param name="appLifetime">Application shutdown cancellation.</param>
    /// <returns>Resume outcome.</returns>
    public async Task<UnitResult<DesktopError>> ResumeAsync(CancellationToken appLifetime)
    {
        _ = state.Pause();
        await operations.WaitAsync().ConfigureAwait(false);
        try
        {
            await run.WaitAsync().ConfigureAwait(false);
        }
        finally
        {
            operations.Release();
        }
        return await StartAsync(appLifetime).ConfigureAwait(false);
    }

    /// <summary>Validates and updates the next acquisition exposure.</summary>
    /// <param name="exposure">Sensor exposure from zero through 255.</param>
    /// <param name="automatic">Enable automatic exposure for valid contact.</param>
    /// <returns>Settings outcome.</returns>
    public Task<UnitResult<DesktopError>> SetExposureAsync(int exposure, bool automatic) =>
        Task.FromResult(state.Exposure.Configure(exposure, automatic));

    /// <summary>Drains acquisition and releases the scanner session once.</summary>
    /// <returns>Shutdown outcome.</returns>
    public async Task<UnitResult<DesktopError>> StopAsync()
    {
        SensorEvidence.Clear();
        _ = state.Pause();
        await shutdown.CancelAsync().ConfigureAwait(false);
        await operations.WaitAsync().ConfigureAwait(false);
        try
        {
            if (stopped)
                return UnitResult.Success<DesktopError>();
            stopped = true;
            await run.WaitAsync().ConfigureAwait(false);
            var closed = await connection.CloseAsync().ConfigureAwait(false);
            run.Dispose();
            return closed;
        }
        finally
        {
            operations.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => new(StopAsync());
}
