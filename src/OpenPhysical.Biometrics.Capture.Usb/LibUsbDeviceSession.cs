// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Usb;

using CSharpFunctionalExtensions;

internal sealed class LibUsbDeviceSession : IAsyncDisposable
{
    private readonly LibUsbNative api;
    private readonly IntPtr handle;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool closed;

    internal LibUsbDeviceSession(LibUsbNative api, IntPtr handle)
    {
        this.api = api;
        this.handle = handle;
    }

    internal static Result<LibUsbDeviceSession, CaptureError> Open(
        LibUsbNative api,
        UsbDeviceInfo target
    ) => LibUsbDeviceCatalog.WithDevices(api, devices => OpenSelected(api, target, devices));

    internal async ValueTask<Result<int, CaptureError>> TransferAsync(
        byte endpoint,
        byte[] buffer,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        if (!ValidBounds(buffer.Length, timeout))
        {
            return Result.Failure<int, CaptureError>(
                new CaptureError(
                    CaptureErrorCodes.InvalidArgument,
                    "USB transfer bounds are invalid."
                )
            );
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (closed)
            {
                return Result.Failure<int, CaptureError>(
                    new CaptureError(CaptureErrorCodes.DeviceUnavailable, "USB device is closed.")
                );
            }

            // Never cancel the worker task itself: close must wait for the bounded physical call.
            return await Task.Run(
                    () => Transfer(endpoint, buffer, (uint)Math.Ceiling(timeout.TotalMilliseconds)),
                    CancellationToken.None
                )
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (closed)
            {
                return;
            }

            closed = true;
            await UsbEdge
                .RunAsync(() => ValueTask.FromResult(CloseHandle()), CancellationToken.None)
                .ConfigureAwait(false);
            await api.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static Result<LibUsbDeviceSession, CaptureError> OpenSelected(
        LibUsbNative api,
        UsbDeviceInfo target,
        System.Collections.Immutable.ImmutableArray<(IntPtr Handle, UsbDeviceInfo Info)> devices
    )
    {
        foreach (var device in devices)
        {
            if (device.Info != target)
            {
                continue;
            }

            if (api.Open(device.Handle, out var handle) != 0 || handle == IntPtr.Zero)
            {
                return Result.Failure<LibUsbDeviceSession, CaptureError>(
                    new CaptureError(CaptureErrorCodes.DeviceUnavailable, "USB open failed.")
                );
            }

            return ClaimOpened(api, handle);
        }

        return Result.Failure<LibUsbDeviceSession, CaptureError>(
            new CaptureError(CaptureErrorCodes.DeviceUnavailable, "Selected USB device is absent.")
        );
    }

    private static Result<LibUsbDeviceSession, CaptureError> ClaimOpened(
        LibUsbNative api,
        IntPtr handle
    )
    {
        bool retained = false;
        try
        {
            if (api.Claim(handle, 0) != 0)
            {
                return Result.Failure<LibUsbDeviceSession, CaptureError>(
                    new CaptureError(CaptureErrorCodes.DeviceBusy, "USB interface is unavailable.")
                );
            }

            var session = new LibUsbDeviceSession(api, handle);
            retained = true;
            return session;
        }
        finally
        {
            if (!retained)
            {
                api.Close(handle);
            }
        }
    }

    private static bool ValidBounds(int length, TimeSpan timeout) =>
        length is >= 1 and <= 65536
        && timeout > TimeSpan.Zero
        && timeout <= TimeSpan.FromSeconds(5);

    private Result<int, CaptureError> Transfer(byte endpoint, byte[] buffer, uint timeout)
    {
        int status = api.BulkTransfer(
            handle,
            endpoint,
            buffer,
            buffer.Length,
            out var count,
            timeout
        );
        if (status != 0 || count < 0 || count > buffer.Length)
        {
            return Result.Failure<int, CaptureError>(
                new CaptureError(CaptureErrorCodes.DeviceUnavailable, "USB transfer failed.")
            );
        }

        return count;
    }

    private Result<bool, CaptureError> CloseHandle()
    {
        try
        {
            api.Release(handle, 0);
            return true;
        }
        finally
        {
            api.Close(handle);
        }
    }
}
