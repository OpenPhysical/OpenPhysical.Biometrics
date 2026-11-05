// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

/// <summary>Direct FS50 acquisition; factory correction remains physically unqualified.</summary>
/// <param name="provider">Exclusive USB discovery/open boundary.</param>
/// <param name="timeProvider">Clock for capture timestamps and bounded session lifetime.</param>
public sealed class Fs50CaptureDriver(IUsbDeviceProvider provider, TimeProvider timeProvider)
    : IFingerprintCaptureDriver
{
    /// <summary>Stable driver identifier.</summary>
    public const string Identifier = "futronic-fs50";

    /// <inheritdoc/>
    public string DriverId => Identifier;

    internal static CaptureCapabilities Capabilities { get; } =
        CaptureCapabilities.Create(0, 255, false, true).Value;

    /// <inheritdoc/>
    public Task<Result<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>> DiscoverAsync(
        CancellationToken cancellationToken
    ) => UsbEdge.RunAsync(() => DiscoverCoreAsync(cancellationToken), cancellationToken).AsTask();

    private async ValueTask<
        Result<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>
    > DiscoverCoreAsync(CancellationToken cancellationToken)
    {
        var devices = await provider
            .DiscoverAsync(0x1491, 0x0050, cancellationToken)
            .ConfigureAwait(false);
        if (devices.IsFailure)
            return devices.Error;
        if (
            devices.Value.IsDefault
            || devices.Value.Length > 256
            || devices.Value.Select(x => x.DeviceId).Distinct(StringComparer.Ordinal).Count()
                != devices.Value.Length
            || devices.Value.Any(x => x is null || x.VendorId != 0x1491 || x.ProductId != 0x0050)
        )
            return new CaptureError(
                CaptureErrorCodes.DeviceUnavailable,
                "Invalid FS50 USB inventory."
            );
        return devices.Value.Select(x => Describe(x).Value).ToImmutableArray();
    }

    /// <inheritdoc/>
    public Task<Result<IFingerprintCaptureSession, CaptureError>> OpenAsync(
        string deviceId,
        CaptureSettings settings,
        CancellationToken cancellationToken
    ) =>
        new Fs50SessionFactory(provider, timeProvider).OpenAsync(
            deviceId,
            settings,
            cancellationToken
        );

    internal static Result<CaptureDeviceDescriptor, CaptureError> Describe(UsbDeviceInfo device) =>
        CaptureDeviceDescriptor.Create(
            device.DeviceId,
            Identifier,
            "Futronic FS50",
            Maybe<string>.None,
            Capabilities
        );
}
