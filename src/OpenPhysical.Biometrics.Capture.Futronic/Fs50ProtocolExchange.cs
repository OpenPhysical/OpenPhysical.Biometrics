// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

internal sealed class Fs50ProtocolExchange
{
    private readonly IUsbDeviceTransport transport;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool faulted;

    internal Fs50ProtocolExchange(IUsbDeviceTransport transport) => this.transport = transport;

    internal ValueTask<Result<ImmutableArray<byte>, CaptureError>> ExecuteAsync(
        ImmutableArray<byte> command,
        int responseBytes,
        CancellationToken cancellationToken
    ) =>
        UsbEdge.RunAsync(
            () => ExecuteOwnedAsync(command, responseBytes, cancellationToken),
            cancellationToken
        );

    private async ValueTask<Result<ImmutableArray<byte>, CaptureError>> ExecuteOwnedAsync(
        ImmutableArray<byte> command,
        int responseBytes,
        CancellationToken cancellationToken
    )
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(30));
        await gate.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try
        {
            if (faulted)
            {
                return Result.Failure<ImmutableArray<byte>, CaptureError>(
                    new CaptureError(
                        CaptureErrorCodes.DeviceUnavailable,
                        "FS50 session requires reopening after a failed transfer."
                    )
                );
            }

            return await WriteAndReadAsync(command, responseBytes, lifetime.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<Result<ImmutableArray<byte>, CaptureError>> WriteAndReadAsync(
        ImmutableArray<byte> command,
        int responseBytes,
        CancellationToken cancellationToken
    )
    {
        // A partial command/reply cannot be resumed safely on the same endpoint stream.
        faulted = true;
        var written = await transport
            .WriteAsync(1, command, TimeSpan.FromSeconds(3), cancellationToken)
            .ConfigureAwait(false);
        if (written.IsFailure)
        {
            return Result.Failure<ImmutableArray<byte>, CaptureError>(written.Error);
        }

        if (written.Value != command.Length)
        {
            return Incomplete();
        }

        var response = await ReadResponseAsync(responseBytes, cancellationToken)
            .ConfigureAwait(false);
        faulted = response.IsFailure;
        return response;
    }

    private async ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadResponseAsync(
        int size,
        CancellationToken cancellationToken
    )
    {
        var bytes = new byte[size];
        var offset = 0;
        try
        {
            while (offset < size && !cancellationToken.IsCancellationRequested)
            {
                int requested = Math.Min(65536, ((size - offset + 511) / 512) * 512);
                var part = await transport
                    .ReadAsync(0x82, requested, TimeSpan.FromSeconds(5), cancellationToken)
                    .ConfigureAwait(false);
                if (part.IsFailure)
                {
                    return part;
                }

                if (!IsCompleteTransfer(part.Value, size - offset, requested))
                {
                    return Incomplete();
                }

                int copied = Math.Min(size - offset, part.Value.Length);
                part.Value.AsSpan(0, copied).CopyTo(bytes.AsSpan(offset));
                offset += copied;
            }

            return Completed(bytes, cancellationToken);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private static Result<ImmutableArray<byte>, CaptureError> Completed(
        byte[] bytes,
        CancellationToken cancellationToken
    ) =>
        cancellationToken.IsCancellationRequested
            ? Result.Failure<ImmutableArray<byte>, CaptureError>(
                new CaptureError(CaptureErrorCodes.Cancelled, "FS50 transfer cancelled.")
            )
            : ImmutableArray.CreateRange(bytes);

    private static Result<ImmutableArray<byte>, CaptureError> Incomplete() =>
        Result.Failure<ImmutableArray<byte>, CaptureError>(
            new CaptureError(
                CaptureErrorCodes.TransferIncomplete,
                "Incomplete or oversized FS50 transfer."
            )
        );

    private static bool IsCompleteTransfer(
        ImmutableArray<byte> bytes,
        int remaining,
        int requested
    ) =>
        !bytes.IsDefaultOrEmpty
        && bytes.Length <= requested
        && bytes.Length >= Math.Min(remaining, requested);
}
