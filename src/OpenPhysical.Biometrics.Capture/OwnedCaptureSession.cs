// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

internal sealed class OwnedCaptureSession : IFingerprintCaptureSession
{
    private readonly IFingerprintCaptureSession inner;
    private readonly CaptureDeviceDescriptor device;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationTokenSource timeout;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object closeLock = new();
    private CaptureSettings settings;
    private Maybe<Task> closeTask;
    private CancellationTokenRegistration cancellationRegistration;
    private long lastSequence = -1;
    private int closed;

    internal OwnedCaptureSession(
        IFingerprintCaptureSession inner,
        CaptureDeviceDescriptor device,
        CaptureSettings settings,
        CancellationTokenSource lifetime,
        CancellationTokenSource timeout
    )
    {
        this.inner = inner;
        this.device = device;
        this.settings = settings;
        this.lifetime = lifetime;
        this.timeout = timeout;
    }

    public string DeviceId => device.DeviceId;

    public async Task<Result<CaptureFrame, CaptureError>> CaptureAsync(
        CancellationToken cancellationToken
    )
    {
        if (Volatile.Read(ref closed) != 0 || cancellationToken.IsCancellationRequested)
        {
            await DisposeAsync().ConfigureAwait(false);
            return Cancelled();
        }
        if (!gate.Wait(0))
            return new CaptureError(
                CaptureErrorCodes.DeviceBusy,
                "A session operation is already in progress."
            );
        Result<CaptureFrame, CaptureError> result;
        try
        {
            result = await CaptureCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
        if (result.IsFailure)
            await DisposeAsync().ConfigureAwait(false);
        return result;
    }

    public async Task<UnitResult<CaptureError>> ApplySettingsAsync(
        CaptureSettings settings,
        CancellationToken cancellationToken
    )
    {
        if (Volatile.Read(ref closed) != 0 || cancellationToken.IsCancellationRequested)
        {
            await DisposeAsync().ConfigureAwait(false);
            return UnitResult.Failure(Cancelled());
        }
        var supported = device.Capabilities.Validate(settings);
        if (supported.IsFailure)
            return supported;
        if (!gate.Wait(0))
            return UnitResult.Failure(
                new CaptureError(
                    CaptureErrorCodes.DeviceBusy,
                    "A session operation is already in progress."
                )
            );
        UnitResult<CaptureError> result;
        try
        {
            result = await ApplyCoreAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
        if (result.IsFailure)
            await DisposeAsync().ConfigureAwait(false);
        return result;
    }

    public ValueTask DisposeAsync()
    {
        lock (closeLock)
        {
            if (!closeTask.HasValue)
            {
                Interlocked.Exchange(ref closed, 1);
                closeTask = CloseCoreAsync();
            }
            return new ValueTask(closeTask.Value);
        }
    }

    internal bool ObserveCancellation()
    {
        cancellationRegistration = lifetime.Token.Register(() =>
        {
            _ = DisposeAsync();
        });
        return true;
    }

    private async Task<Result<CaptureFrame, CaptureError>> CaptureCoreAsync(
        CancellationToken cancellationToken
    )
    {
        if (Volatile.Read(ref closed) != 0)
            return Cancelled();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            lifetime.Token,
            cancellationToken
        );
        var result = await CaptureEdge
            .RunAsync(() => inner.CaptureAsync(linked.Token))
            .ConfigureAwait(false);
        if (result.IsFailure)
            return result;
        if (linked.IsCancellationRequested)
            return Cancelled();
        var frame = result.Value;
        if (
            frame is null
            || frame.Provenance.Device != device
            || frame.Provenance.Settings != settings
            || frame.Sequence <= lastSequence
        )
            return new CaptureError(
                CaptureErrorCodes.InvalidFrame,
                "The frame does not match its device, settings or session sequence."
            );
        lastSequence = frame.Sequence;
        return result;
    }

    private async Task<UnitResult<CaptureError>> ApplyCoreAsync(
        CaptureSettings next,
        CancellationToken cancellationToken
    )
    {
        if (Volatile.Read(ref closed) != 0)
            return UnitResult.Failure(Cancelled());
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            lifetime.Token,
            cancellationToken
        );
        var result = await CaptureEdge
            .ApplyAsync(() => inner.ApplySettingsAsync(next, linked.Token))
            .ConfigureAwait(false);
        if (linked.IsCancellationRequested)
            return UnitResult.Failure(Cancelled());
        if (result.IsSuccess)
            settings = next;
        return result;
    }

    private async Task CloseCoreAsync()
    {
        await Task.Yield();
        await CaptureEdge
            .ApplyAsync(async () =>
            {
                await lifetime.CancelAsync().ConfigureAwait(false);
                return UnitResult.Success<CaptureError>();
            })
            .ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var result = await CaptureEdge.DisposeAsync(inner).ConfigureAwait(false);
            // A failed close quarantines the physical lease until process restart.
            if (result.IsSuccess)
                CaptureDriverRegistry.Release(DeviceId);
        }
        finally
        {
            gate.Release();
            cancellationRegistration.Unregister();
            lifetime.Dispose();
            timeout.Dispose();
        }
    }

    private static CaptureError Cancelled() =>
        new(CaptureErrorCodes.Cancelled, "The acquisition session was cancelled or closed.");
}
