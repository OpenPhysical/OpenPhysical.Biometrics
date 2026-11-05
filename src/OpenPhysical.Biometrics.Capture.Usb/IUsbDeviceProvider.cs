// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Usb;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Discovery and exclusive opening boundary for a configured USB runtime.</summary>
public interface IUsbDeviceProvider
{
    /// <summary>Enumerates matching devices without claiming interfaces.</summary>
    /// <param name="vendorId">USB vendor.</param>
    /// <param name="productId">USB product.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Current addresses or a typed failure.</returns>
    ValueTask<Result<ImmutableArray<UsbDeviceInfo>, CaptureError>> DiscoverAsync(
        ushort vendorId,
        ushort productId,
        CancellationToken cancellationToken
    );

    /// <summary>Claims one current device exclusively.</summary>
    /// <param name="device">Current address.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>An owned transport or a typed failure.</returns>
    ValueTask<Result<IUsbDeviceTransport, CaptureError>> OpenAsync(
        UsbDeviceInfo device,
        CancellationToken cancellationToken
    );
}
