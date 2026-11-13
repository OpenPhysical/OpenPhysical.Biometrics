// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

internal sealed class AvFoundationCameraSession : ICameraSession
{
    private readonly CameraFrameBuffer frames = new();
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly object gate = new();
    private readonly CameraSource source;
    private readonly CameraMode mode;
    private readonly AvFoundationNative.FrameCallback callback;
    private nint handle;
    private GCHandle callbackRoot;
    private Maybe<CameraError> fault;
    private long sequence;

    private AvFoundationCameraSession(nint handle, CameraSource source, CameraMode mode)
    {
        this.handle = handle;
        this.source = source;
        this.mode = mode;
        callback = Arrived;
        callbackRoot = GCHandle.Alloc(this);
    }

    internal static Result<ICameraSession, CameraError> Open(string sourceId, CameraMode mode)
    {
        // Catalog reads do not change any device's active format.
        var sources = AvFoundationCatalog.Read();
        if (sources.IsFailure)
            return sources.Error;
        var source = sources
            .Value.Where(value => value.Id == sourceId)
            .Select(Maybe.From)
            .FirstOrDefault();
        if (source.HasNoValue)
            return AvFoundationStatus.Error(2);
        if (!source.Value.Modes.Contains(mode))
            return AvFoundationStatus.Error(3);
        var status = AvFoundationNative.opbc_open(
            sourceId,
            mode.Width,
            mode.Height,
            mode.FrameRateNumerator,
            mode.FrameRateDenominator,
            out var handle
        );
        if (status != 0)
            return AvFoundationStatus.Error(status);
        if (handle == nint.Zero)
            return AvFoundationStatus.Error(5);
        var session = new AvFoundationCameraSession(handle, source.Value, mode);
        var started = AvFoundationNative.opbc_start(
            handle,
            session.callback,
            GCHandle.ToIntPtr(session.callbackRoot)
        );
        if (started == 0)
            return Result.Success<ICameraSession, CameraError>(session);
        session.CloseNative();
        return AvFoundationStatus.Error(started);
    }

    private void Arrived(
        nint context,
        nint pointer,
        nuint length,
        int width,
        int height,
        nuint stride,
        long timestampNanoseconds,
        int status
    )
    {
        var copied =
            status == 0
                ? CameraEdge.Run(() =>
                    AvFoundationFrameCopy.Copy(pointer, length, width, height, stride, mode)
                )
                : Result.Failure<OpenPhysical.Biometrics.Imaging.RgbImage, CameraError>(
                    AvFoundationStatus.Error(status)
                );
        if (copied.IsSuccess)
            frames.Publish(
                new(
                    copied.Value,
                    source,
                    mode,
                    Interlocked.Increment(ref sequence),
                    DateTimeOffset.UtcNow,
                    TimeSpan.FromTicks(timestampNanoseconds / 100)
                )
            );
        else
        {
            lock (gate)
                fault = copied.Error;
            frames.Complete();
        }
    }

    public async Task<Result<CameraFrame, CameraError>> ReadAsync(CancellationToken token)
    {
        var read = await frames.ReadAsync(token).ConfigureAwait(false);
        lock (gate)
            return read.IsFailure && fault.HasValue ? fault.Value : read;
    }

    public async Task<UnitResult<CameraError>> StopAsync(CancellationToken token)
    {
        frames.Complete();
        await operations.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var closed = await CameraEdge
                .RunAsync(async () =>
                    (await Task.Run(CloseNative).ConfigureAwait(false)).Map(() => true)
                )
                .ConfigureAwait(false);
            return closed.IsSuccess ? UnitResult.Success<CameraError>() : closed.Error;
        }
        finally
        {
            operations.Release();
        }
    }

    private UnitResult<CameraError> CloseNative()
    {
        var native = handle;
        if (native == nint.Zero)
            return UnitResult.Success<CameraError>();
        handle = nint.Zero;
        var stopped = AvFoundationNative.opbc_stop(native);
        AvFoundationNative.opbc_release(native);
        GC.KeepAlive(callback);
        callbackRoot.Free();
        return AvFoundationStatus.Check(stopped);
    }

    public async ValueTask DisposeAsync() =>
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
}
