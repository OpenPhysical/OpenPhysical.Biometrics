// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using CSharpFunctionalExtensions;

/// <summary>A one-frame acquisition mailbox that clears superseded and late frame pixels.</summary>
public sealed class CameraFrameBuffer
{
    private readonly object sync = new();
    private readonly SemaphoreSlim available = new(0, 1);
    private readonly SemaphoreSlim readers = new(1, 1);
    private Maybe<CameraFrame> frame;
    private bool closed;

    /// <summary>Transfers a frame into the queue, clearing the replaced frame or rejecting it after close.</summary>
    /// <param name="value">Frame whose ownership always transfers to this buffer.</param>
    /// <returns>Publication success or a stopped-session failure.</returns>
    public UnitResult<CameraError> Publish(CameraFrame value)
    {
        lock (sync)
        {
            if (closed)
            {
                value.Dispose();
                return UnitResult.Failure(Closed());
            }
            frame.Tap(previous => previous.Dispose());
            frame = value;
            if (available.CurrentCount == 0)
                available.Release();
            return UnitResult.Success<CameraError>();
        }
    }

    /// <summary>Reads one owned frame, with serialized callers and a bounded disconnection deadline.</summary>
    /// <param name="token">Read cancellation.</param>
    /// <returns>The frame the caller disposes, or a typed timeout/closed/cancellation failure.</returns>
    public Task<Result<CameraFrame, CameraError>> ReadAsync(CancellationToken token) =>
        CameraEdge.RunAsync(async () =>
        {
            await readers.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await ReadNextAsync(token).ConfigureAwait(false);
            }
            finally
            {
                readers.Release();
            }
        });

    private async Task<Result<CameraFrame, CameraError>> ReadNextAsync(CancellationToken token)
    {
        lock (sync)
            if (closed)
                return Closed();
        while (await available.WaitAsync(TimeSpan.FromSeconds(3), token).ConfigureAwait(false))
        {
            lock (sync)
            {
                if (closed)
                    return Closed();
                if (frame.HasNoValue)
                    continue;
                var value = frame.Value;
                frame = default;
                return value;
            }
        }
        return new CameraError(
            CameraErrorCodes.FrameTimeout,
            "The camera stopped delivering frames. Check its connection and try again."
        );
    }

    /// <summary>Releases queued pixels and fences all later publication without cancelling a reader's owner.</summary>
    /// <returns>Idempotent completion success.</returns>
    public UnitResult<CameraError> Complete()
    {
        lock (sync)
        {
            closed = true;
            frame.Tap(value => value.Dispose());
            frame = default;
            if (available.CurrentCount == 0)
                available.Release();
            return UnitResult.Success<CameraError>();
        }
    }

    private static CameraError Closed() =>
        new(CameraErrorCodes.Closed, "Camera capture has stopped.");
}
