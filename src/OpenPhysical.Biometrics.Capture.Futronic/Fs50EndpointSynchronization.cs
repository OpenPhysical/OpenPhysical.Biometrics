// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

internal static class Fs50EndpointSynchronization
{
    internal static async ValueTask<UnitResult<CaptureError>> DrainAsync(
        IUsbDeviceTransport transport,
        CancellationToken caller
    )
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(caller);
        budget.CancelAfter(TimeSpan.FromSeconds(2));
        var consumed = 0;
        for (var reads = 0; reads < 64 && !budget.IsCancellationRequested; reads++)
        {
            var read = await transport
                .ReadAsync(0x82, 65536, TimeSpan.FromMilliseconds(100), budget.Token)
                .ConfigureAwait(false);
            if (caller.IsCancellationRequested)
                return new CaptureError(
                    CaptureErrorCodes.Cancelled,
                    "FS50 synchronization cancelled."
                );
            if (budget.IsCancellationRequested)
                return Failed();
            if (read.IsFailure)
            {
                if (read.Error.Code == CaptureErrorCodes.TransferTimedOut)
                    return UnitResult.Success<CaptureError>();
                return
                    read.Error.Code == CaptureErrorCodes.Cancelled && budget.IsCancellationRequested
                    ? Failed()
                    : UnitResult.Failure(read.Error);
            }
            if (read.Value.IsDefault || read.Value.Length > 65536)
                return Failed();
            consumed += read.Value.Length;
            if (consumed > 2 * Fs50Descriptor.RawWidth * Fs50Descriptor.RawHeight)
                return Failed();
        }
        return caller.IsCancellationRequested
            ? new CaptureError(CaptureErrorCodes.Cancelled, "FS50 synchronization cancelled.")
            : Failed();
    }

    private static CaptureError Failed() =>
        new(
            CaptureErrorCodes.SynchronizationFailed,
            "FS50 did not reach a quiet input endpoint within the synchronization budget. Reconnect the scanner before resetting it."
        );
}
