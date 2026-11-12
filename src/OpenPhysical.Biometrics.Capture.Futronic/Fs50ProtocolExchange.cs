// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

internal sealed class Fs50ProtocolExchange(IUsbDeviceTransport transport)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool faulted;

    internal ValueTask<Result<ImmutableArray<byte>, CaptureError>> ExecuteAsync(
        ImmutableArray<byte> command,
        int responseBytes,
        CancellationToken cancellationToken
    ) =>
        UsbEdge.RunAsync(
            () => ExecuteOwnedAsync(command, responseBytes, cancellationToken),
            cancellationToken
        );

    internal async ValueTask<UnitResult<CaptureError>> SynchronizeAsync(CancellationToken caller)
    {
        var result = await UsbEdge
            .RunAsync(
                async () =>
                {
                    if (
                        !await gate.WaitAsync(TimeSpan.FromSeconds(2), caller).ConfigureAwait(false)
                    )
                        return Result.Failure<bool, CaptureError>(
                            new(
                                CaptureErrorCodes.DeviceBusy,
                                "An FS50 transaction is still active."
                            )
                        );
                    try
                    {
                        if (faulted)
                            return Result.Failure<bool, CaptureError>(Unavailable());
                        var drained = await Fs50EndpointSynchronization
                            .DrainAsync(transport, caller)
                            .ConfigureAwait(false);
                        faulted = drained.IsFailure;
                        return drained.IsSuccess
                            ? Result.Success<bool, CaptureError>(true)
                            : Result.Failure<bool, CaptureError>(drained.Error);
                    }
                    finally
                    {
                        gate.Release();
                    }
                },
                caller
            )
            .ConfigureAwait(false);
        return result.IsSuccess
            ? UnitResult.Success<CaptureError>()
            : UnitResult.Failure(result.Error);
    }

    private async ValueTask<Result<ImmutableArray<byte>, CaptureError>> ExecuteOwnedAsync(
        ImmutableArray<byte> command,
        int responseBytes,
        CancellationToken caller
    )
    {
        if (!await gate.WaitAsync(TimeSpan.FromSeconds(30), caller).ConfigureAwait(false))
            return new CaptureError(
                CaptureErrorCodes.TransferTimedOut,
                "FS50 transaction ownership timed out."
            );
        try
        {
            if (faulted)
                return Unavailable();
            return await WriteAndReadAsync(command, responseBytes, caller).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<Result<ImmutableArray<byte>, CaptureError>> WriteAndReadAsync(
        ImmutableArray<byte> command,
        int responseBytes,
        CancellationToken caller
    )
    {
        faulted = true;
        var written = await UsbEdge
            .RunAsync(
                () => transport.WriteAsync(1, command, TimeSpan.FromSeconds(3), caller),
                caller
            )
            .ConfigureAwait(false);
        if (written.IsFailure || written.Value != command.Length)
        {
            _ = await Fs50EndpointSynchronization
                .DrainAsync(transport, CancellationToken.None)
                .ConfigureAwait(false);
            return caller.IsCancellationRequested ? Cancelled()
                : written.IsFailure ? written.Error
                : new CaptureError(
                    CaptureErrorCodes.TransferIncomplete,
                    "Incomplete FS50 command."
                );
        }
        // An issued command owns its complete reply, even after caller/session cancellation.
        var response = await Fs50ResponseReader
            .ReadAsync(transport, responseBytes, caller)
            .ConfigureAwait(false);
        faulted = response.IsFailure;
        if (response.IsFailure && !caller.IsCancellationRequested)
            _ = await Fs50EndpointSynchronization
                .DrainAsync(transport, CancellationToken.None)
                .ConfigureAwait(false);
        return response;
    }

    private static CaptureError Unavailable() =>
        new(
            CaptureErrorCodes.DeviceUnavailable,
            "FS50 session requires reopening after a failed transfer."
        );

    private static CaptureError Cancelled() =>
        new(CaptureErrorCodes.Cancelled, "FS50 command cancelled after bounded endpoint cleanup.");
}
