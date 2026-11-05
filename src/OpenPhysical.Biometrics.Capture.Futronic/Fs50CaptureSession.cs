// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

/// <summary>An exclusive FS50 session with serialized settings and bounded cleanup.</summary>
public sealed class Fs50CaptureSession : IFingerprintCaptureSession
{
    private readonly IUsbDeviceTransport transport;
    private readonly Fs50Protocol protocol;
    private readonly Fs50FrameFactory frames;
    private readonly Fs50SessionLifetime lifetime;
    private readonly TimeProvider clock;
    private CaptureSettings settings;
    private long sequence;

    internal Fs50CaptureSession(
        IUsbDeviceTransport transport,
        Fs50Protocol protocol,
        Fs50FrameFactory frames,
        string deviceId,
        CaptureSettings settings,
        Fs50SessionLifetime lifetime,
        TimeProvider clock
    )
    {
        this.transport = transport;
        this.protocol = protocol;
        this.frames = frames;
        this.settings = settings;
        this.lifetime = lifetime;
        this.clock = clock;
        DeviceId = deviceId;
    }

    /// <inheritdoc/>
    public string DeviceId { get; }

    /// <inheritdoc/>
    public async Task<Result<CaptureFrame, CaptureError>> CaptureAsync(
        CancellationToken cancellationToken
    )
    {
        if (lifetime.IsClosed || cancellationToken.IsCancellationRequested)
        {
            await DisposeAsync().ConfigureAwait(false);
            return Cancelled();
        }
        if (!lifetime.Gate.Wait(0))
            return new CaptureError(
                CaptureErrorCodes.DeviceBusy,
                "FS50 acquisition is already active."
            );
        Result<CaptureFrame, CaptureError> result;
        try
        {
            if (lifetime.IsClosed)
                return Cancelled();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                lifetime.Token,
                cancellationToken
            );
            result = await AcquireAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            lifetime.Gate.Release();
        }
        if (result.IsFailure)
            await DisposeAsync().ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<UnitResult<CaptureError>> ApplySettingsAsync(
        CaptureSettings next,
        CancellationToken cancellationToken
    )
    {
        if (lifetime.IsClosed || cancellationToken.IsCancellationRequested)
        {
            await DisposeAsync().ConfigureAwait(false);
            return UnitResult.Failure(Cancelled());
        }
        var supported = Fs50CaptureDriver.Capabilities.Validate(next);
        if (supported.IsFailure)
            return supported;
        if (!lifetime.Gate.Wait(0))
            return UnitResult.Failure(
                new CaptureError(
                    CaptureErrorCodes.DeviceBusy,
                    "FS50 acquisition is already active."
                )
            );
        bool cancelled;
        try
        {
            cancelled = lifetime.IsClosed || cancellationToken.IsCancellationRequested;
            // FS50 sends exposure with each capture command; no persistent setting write exists.
            if (!cancelled)
                settings = next;
        }
        finally
        {
            lifetime.Gate.Release();
        }
        if (cancelled)
            await DisposeAsync().ConfigureAwait(false);
        return cancelled ? UnitResult.Failure(Cancelled()) : UnitResult.Success<CaptureError>();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => new(lifetime.CloseAsync(transport));

    private async Task<Result<CaptureFrame, CaptureError>> AcquireAsync(CancellationToken token)
    {
        if (lifetime.IsClosed || token.IsCancellationRequested)
            return Cancelled();
        var raw = await protocol.CaptureRawAsync(settings.Exposure, token).ConfigureAwait(false);
        if (raw.IsFailure)
            return raw.Error;
        var timestamp = clock.GetUtcNow();
        var frame = frames.Create(raw.Value, settings, sequence, timestamp);
        if (token.IsCancellationRequested)
            return Cancelled();
        if (frame.IsSuccess)
            sequence++;
        return frame;
    }

    private static CaptureError Cancelled() =>
        new(CaptureErrorCodes.Cancelled, "FS50 session cancelled or closed.");
}
