// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using CSharpFunctionalExtensions;
using FlashCap;

internal sealed class FlashCapCameraSession(CameraSource source, CameraMode mode, bool avFoundation)
    : ICameraSession
{
    private readonly CameraFrameBuffer frames = new();
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly object sync = new();
    private Maybe<CaptureDevice> device;
    private Maybe<CameraError> fault;
    private bool stopped;

    internal async Task<UnitResult<CameraError>> OpenAsync(
        CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristic,
        CancellationToken token
    )
    {
        var opened = await CameraEdge
            .RunAsync(async () =>
                Result.Success<CaptureDevice, CameraError>(
                    await descriptor
                        .OpenAsync(
                            characteristic,
                            TranscodeFormats.DoNotTranscode,
                            false,
                            1,
                            Arrived,
                            token
                        )
                        .ConfigureAwait(false)
                )
            )
            .ConfigureAwait(false);
        if (opened.IsFailure)
            return UnitResult.Failure(opened.Error);
        device = opened.Value;
        var started = await CameraEdge
            .ApplyAsync(() => opened.Value.StartAsync(token))
            .ConfigureAwait(false);
        if (started.IsFailure)
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        return started;
    }

    private Task Arrived(PixelBufferScope scope)
    {
        var result = CameraEdge.Run(() =>
        {
            var bytes = scope.Buffer.ReferImage();
            try
            {
                return CameraBufferDecoder
                    .Decode(bytes.AsMemory(), mode, avFoundation)
                    .Map(image => new CameraFrame(
                        image,
                        source,
                        mode,
                        scope.Buffer.FrameIndex,
                        DateTimeOffset.UtcNow,
                        scope.Buffer.Timestamp
                    ));
            }
            finally
            {
                bytes.AsSpan().Clear();
            }
        });
        if (result.IsSuccess)
            frames.Publish(result.Value);
        else
        {
            lock (sync)
                fault = result.Error;
            frames.Complete();
        }
        return Task.CompletedTask;
    }

    public async Task<Result<CameraFrame, CameraError>> ReadAsync(CancellationToken token)
    {
        var read = await frames.ReadAsync(token).ConfigureAwait(false);
        lock (sync)
            return read.IsFailure && fault.HasValue ? fault.Value : read;
    }

    public async Task<UnitResult<CameraError>> StopAsync(CancellationToken token)
    {
        frames.Complete();
        await operations.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (stopped)
                return UnitResult.Success<CameraError>();
            stopped = true;
            if (device.HasNoValue)
                return UnitResult.Success<CameraError>();
            var current = device.Value;
            device = default;
            var stop = await CameraEdge
                .ApplyAsync(() => current.StopAsync(CancellationToken.None))
                .ConfigureAwait(false);
            var dispose = await CameraEdge
                .ApplyAsync(() => current.DisposeAsync())
                .ConfigureAwait(false);
            return stop.IsFailure ? stop : dispose;
        }
        finally
        {
            operations.Release();
        }
    }

    public async ValueTask DisposeAsync() =>
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
}
