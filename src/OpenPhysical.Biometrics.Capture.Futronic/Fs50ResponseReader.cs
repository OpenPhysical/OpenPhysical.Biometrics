// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

internal static class Fs50ResponseReader
{
    internal static async ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadAsync(
        IUsbDeviceTransport transport,
        int size,
        CancellationToken caller
    )
    {
        var bytes = new byte[size];
        using var transaction = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var cleanup = new CancellationTokenSource();
        using var response = CancellationTokenSource.CreateLinkedTokenSource(
            transaction.Token,
            cleanup.Token
        );
        try
        {
            return await ConsumeAsync(transport, bytes, caller, cleanup, response.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private static async ValueTask<Result<ImmutableArray<byte>, CaptureError>> ConsumeAsync(
        IUsbDeviceTransport transport,
        byte[] bytes,
        CancellationToken caller,
        CancellationTokenSource cleanup,
        CancellationToken response
    )
    {
        var offset = 0;
        var draining = false;
        for (var reads = 0; offset < bytes.Length && reads < 64; reads++)
        {
            if (caller.IsCancellationRequested && !draining)
            {
                draining = true;
                cleanup.CancelAfter(TimeSpan.FromSeconds(2));
            }
            var read = await ReadPartAsync(transport, bytes, offset, draining, caller, response)
                .ConfigureAwait(false);
            if (read.IsFailure)
                return caller.IsCancellationRequested ? Cancelled() : read.Error;
            offset += read.Value;
        }
        if (caller.IsCancellationRequested)
            return Cancelled();
        return offset == bytes.Length
            ? ImmutableArray.CreateRange(bytes)
            : new CaptureError(
                CaptureErrorCodes.TransferIncomplete,
                "FS50 response exceeded the bounded read count."
            );
    }

    private static async ValueTask<Result<int, CaptureError>> ReadPartAsync(
        IUsbDeviceTransport transport,
        byte[] bytes,
        int offset,
        bool draining,
        CancellationToken caller,
        CancellationToken response
    )
    {
        int remaining = bytes.Length - offset;
        int requested = Math.Min(65536, ((remaining + 511) / 512) * 512);
        var part = await transport
            .ReadAsync(
                0x82,
                requested,
                draining ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(5),
                response
            )
            .ConfigureAwait(false);
        if (response.IsCancellationRequested)
            return new CaptureError(
                CaptureErrorCodes.TransferTimedOut,
                "FS50 response exceeded its transaction or cleanup deadline."
            );
        if (part.IsFailure)
            return Failure(part.Error, caller, response);
        if (
            part.Value.IsDefaultOrEmpty
            || part.Value.Length > requested
            || !caller.IsCancellationRequested && part.Value.Length < Math.Min(remaining, requested)
        )
            return new CaptureError(
                CaptureErrorCodes.TransferIncomplete,
                "Incomplete or oversized FS50 transfer."
            );
        int copied = Math.Min(remaining, part.Value.Length);
        if (!caller.IsCancellationRequested)
            part.Value.AsSpan(0, copied).CopyTo(bytes.AsSpan(offset));
        return copied;
    }

    private static Result<int, CaptureError> Failure(
        CaptureError error,
        CancellationToken caller,
        CancellationToken response
    )
    {
        if (
            caller.IsCancellationRequested
            && !response.IsCancellationRequested
            && error.Code == CaptureErrorCodes.TransferTimedOut
        )
            return 0;
        return response.IsCancellationRequested && !caller.IsCancellationRequested
            ? new CaptureError(
                CaptureErrorCodes.TransferTimedOut,
                "FS50 response exceeded its transaction deadline."
            )
            : error;
    }

    private static CaptureError Cancelled() =>
        new(CaptureErrorCodes.Cancelled, "FS50 transfer cancelled after bounded reply cleanup.");
}
