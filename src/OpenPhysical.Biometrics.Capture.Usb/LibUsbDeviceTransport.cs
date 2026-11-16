// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Usb;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Explicit-path libusb transport with bounded transfers and serialized native lifetime.</summary>
public sealed class LibUsbDeviceTransport : IUsbDeviceTransport
{
    private readonly LibUsbDeviceSession session;

    private LibUsbDeviceTransport(LibUsbDeviceSession session) => this.session = session;

    /// <summary>Enumerates current addresses without claiming any interface.</summary>
    /// <param name="libraryPath">Approved absolute libusb library path.</param>
    /// <param name="vendorId">Requested USB vendor.</param>
    /// <param name="productId">Requested USB product.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching current device addresses or a safe native error.</returns>
    public static ValueTask<Result<ImmutableArray<UsbDeviceInfo>, CaptureError>> DiscoverAsync(
        string libraryPath,
        ushort vendorId,
        ushort productId,
        CancellationToken cancellationToken
    ) =>
        UsbEdge.RunAsync(
            async () =>
            {
                var loaded = LibUsbNative.Load(libraryPath);
                if (loaded.IsFailure)
                {
                    return Result.Failure<ImmutableArray<UsbDeviceInfo>, CaptureError>(
                        loaded.Error
                    );
                }

                await using var api = loaded.Value;
                return LibUsbDeviceCatalog.WithDevices(
                    api,
                    devices =>
                        Result.Success<ImmutableArray<UsbDeviceInfo>, CaptureError>(
                            devices
                                .Where(x =>
                                    x.Info.VendorId == vendorId && x.Info.ProductId == productId
                                )
                                .Select(x => x.Info)
                                .ToImmutableArray()
                        )
                );
            },
            cancellationToken
        );

    /// <summary>Opens exactly the requested enumeration and claims interface zero.</summary>
    /// <param name="libraryPath">Approved absolute libusb library path.</param>
    /// <param name="device">Current device address, revalidated before opening.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>An owned transport or a safe open/claim failure.</returns>
    public static ValueTask<Result<LibUsbDeviceTransport, CaptureError>> OpenAsync(
        string libraryPath,
        UsbDeviceInfo device,
        CancellationToken cancellationToken
    ) =>
        UsbEdge.RunAsync(
            () => OpenCoreAsync(libraryPath, device, cancellationToken),
            cancellationToken
        );

    /// <inheritdoc/>
    public ValueTask<Result<int, CaptureError>> WriteAsync(
        byte endpoint,
        ImmutableArray<byte> data,
        TimeSpan timeout,
        CancellationToken cancellationToken
    ) =>
        UsbEdge.RunAsync(
            () => WriteCoreAsync(endpoint, data, timeout, cancellationToken),
            cancellationToken
        );

    /// <inheritdoc/>
    public ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadAsync(
        byte endpoint,
        int maximumBytes,
        TimeSpan timeout,
        CancellationToken cancellationToken
    ) =>
        UsbEdge.RunAsync(
            () => ReadCoreAsync(endpoint, maximumBytes, timeout, cancellationToken),
            cancellationToken
        );

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => session.DisposeAsync();

    private static async ValueTask<Result<LibUsbDeviceTransport, CaptureError>> OpenCoreAsync(
        string libraryPath,
        UsbDeviceInfo device,
        CancellationToken cancellationToken
    )
    {
        var loaded = LibUsbNative.Load(libraryPath);
        if (loaded.IsFailure)
        {
            return Result.Failure<LibUsbDeviceTransport, CaptureError>(loaded.Error);
        }

        bool retained = false;
        try
        {
            var opened = LibUsbDeviceSession.Open(loaded.Value, device);
            if (opened.IsFailure)
            {
                return Result.Failure<LibUsbDeviceTransport, CaptureError>(opened.Error);
            }

            retained = true;
            if (cancellationToken.IsCancellationRequested)
            {
                await opened.Value.DisposeAsync().ConfigureAwait(false);
                return Result.Failure<LibUsbDeviceTransport, CaptureError>(
                    new CaptureError(CaptureErrorCodes.Cancelled, "USB open cancelled.")
                );
            }

            return new LibUsbDeviceTransport(opened.Value);
        }
        finally
        {
            if (!retained)
            {
                await loaded.Value.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<Result<int, CaptureError>> WriteCoreAsync(
        byte endpoint,
        ImmutableArray<byte> data,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        if (
            (endpoint & 0x80) != 0
            || (endpoint & 0x0F) == 0
            || data.IsDefaultOrEmpty
            || data.Length > 65536
        )
        {
            return Result.Failure<int, CaptureError>(
                new CaptureError(CaptureErrorCodes.InvalidArgument, "Invalid USB output transfer.")
            );
        }

        var bytes = data.ToArray();
        try
        {
            return await session
                .TransferAsync(endpoint, bytes, timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private async ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadCoreAsync(
        byte endpoint,
        int maximumBytes,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        if ((endpoint & 0xF0) != 0x80 || (endpoint & 0x0F) == 0 || maximumBytes is < 1 or > 65536)
        {
            return Result.Failure<ImmutableArray<byte>, CaptureError>(
                new CaptureError(CaptureErrorCodes.InvalidArgument, "Invalid USB input transfer.")
            );
        }

        var bytes = new byte[maximumBytes];
        try
        {
            var read = await session
                .TransferAsync(endpoint, bytes, timeout, cancellationToken)
                .ConfigureAwait(false);
            return read.IsFailure
                ? Result.Failure<ImmutableArray<byte>, CaptureError>(read.Error)
                : ImmutableArray.CreateRange(bytes.AsSpan(0, read.Value).ToArray());
        }
        finally
        {
            Array.Clear(bytes);
        }
    }
}
