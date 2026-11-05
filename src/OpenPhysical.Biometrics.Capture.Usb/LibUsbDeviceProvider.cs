// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Usb;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>USB provider using an explicitly configured native library path.</summary>
/// <param name="libraryPath">Approved absolute libusb library path.</param>
public sealed class LibUsbDeviceProvider(string libraryPath) : IUsbDeviceProvider
{
    /// <inheritdoc/>
    public ValueTask<Result<ImmutableArray<UsbDeviceInfo>, CaptureError>> DiscoverAsync(
        ushort vendorId,
        ushort productId,
        CancellationToken cancellationToken
    ) => LibUsbDeviceTransport.DiscoverAsync(libraryPath, vendorId, productId, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<Result<IUsbDeviceTransport, CaptureError>> OpenAsync(
        UsbDeviceInfo device,
        CancellationToken cancellationToken
    )
    {
        var result = await LibUsbDeviceTransport
            .OpenAsync(libraryPath, device, cancellationToken)
            .ConfigureAwait(false);
        return result.Map(transport => (IUsbDeviceTransport)transport);
    }
}
