// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Usb;

namespace OpenPhysical.Biometrics.Desktop;

// Keep the workspace available so an unavailable scanner cannot prevent other enrollment work.
internal sealed class UnavailableUsbDeviceProvider(CaptureError error) : IUsbDeviceProvider
{
    public ValueTask<Result<ImmutableArray<UsbDeviceInfo>, CaptureError>> DiscoverAsync(
        ushort vendorId,
        ushort productId,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(Result.Failure<ImmutableArray<UsbDeviceInfo>, CaptureError>(error));

    public ValueTask<Result<IUsbDeviceTransport, CaptureError>> OpenAsync(
        UsbDeviceInfo device,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(Result.Failure<IUsbDeviceTransport, CaptureError>(error));
}
