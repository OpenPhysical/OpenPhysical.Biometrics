// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

internal sealed class Fs50SessionFactory(IUsbDeviceProvider provider, TimeProvider clock)
{
    internal async Task<Result<IFingerprintCaptureSession, CaptureError>> OpenAsync(
        string deviceId,
        CaptureSettings settings,
        CancellationToken token
    )
    {
        var supported = Fs50CaptureDriver.Capabilities.Validate(settings);
        if (supported.IsFailure)
            return supported.Error;
        var lifetime = new Fs50SessionLifetime(settings.Lifetime, clock, token);
        var result = await UsbEdge
            .RunAsync(() => OpenWithinLifetimeAsync(deviceId, settings, lifetime), lifetime.Token)
            .ConfigureAwait(false);
        if (result.IsFailure)
            await lifetime.DisposeAsync().ConfigureAwait(false);
        return result;
    }

    private async ValueTask<
        Result<IFingerprintCaptureSession, CaptureError>
    > OpenWithinLifetimeAsync(
        string deviceId,
        CaptureSettings settings,
        Fs50SessionLifetime lifetime
    )
    {
        var found = await provider
            .DiscoverAsync(0x1491, 0x0050, lifetime.Token)
            .ConfigureAwait(false);
        if (found.IsFailure)
            return found.Error;
        if (
            found.Value.IsDefault
            || found.Value.Count(x =>
                x is not null
                && x.DeviceId == deviceId
                && x.VendorId == 0x1491
                && x.ProductId == 0x0050
            ) != 1
        )
            return new CaptureError(
                CaptureErrorCodes.DeviceUnavailable,
                "FS50 device is no longer current."
            );
        var address = found.Value.First(x => x.DeviceId == deviceId);
        var opened = await provider.OpenAsync(address, lifetime.Token).ConfigureAwait(false);
        if (opened.IsFailure)
            return opened.Error;
        return await InitializeAsync(opened.Value, address, settings, lifetime)
            .ConfigureAwait(false);
    }

    private async Task<Result<IFingerprintCaptureSession, CaptureError>> InitializeAsync(
        IUsbDeviceTransport transport,
        UsbDeviceInfo address,
        CaptureSettings settings,
        Fs50SessionLifetime lifetime
    )
    {
        bool retained = false;
        try
        {
            var protocol = new Fs50Protocol(transport);
            var frames = await ReadGeometryAsync(
                    protocol,
                    Fs50CaptureDriver.Describe(address).Value,
                    lifetime.Token
                )
                .ConfigureAwait(false);
            if (frames.IsFailure)
                return frames.Error;
            if (lifetime.IsClosed)
                return new CaptureError(CaptureErrorCodes.Cancelled, "FS50 opening cancelled.");
            var session = new Fs50CaptureSession(
                transport,
                protocol,
                frames.Value,
                address.DeviceId,
                settings,
                lifetime,
                clock
            );
            retained = true;
            lifetime.Observe(transport);
            return Result.Success<IFingerprintCaptureSession, CaptureError>(session);
        }
        finally
        {
            if (!retained)
                await transport.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<Result<Fs50FrameFactory, CaptureError>> ReadGeometryAsync(
        Fs50Protocol protocol,
        CaptureDeviceDescriptor device,
        CancellationToken token
    )
    {
        var descriptor = await protocol.ReadDescriptorAsync(token).ConfigureAwait(false);
        if (descriptor.IsFailure)
            return descriptor.Error;
        var valid = Fs50Descriptor.Validate(descriptor.Value.AsSpan());
        if (valid.IsFailure)
            return valid.Error;
        var controls = await protocol.ReadControlsAsync(token).ConfigureAwait(false);
        if (controls.IsFailure)
            return controls.Error;
        var calibration = await protocol.ReadCalibrationAsync(token).ConfigureAwait(false);
        if (calibration.IsFailure)
            return calibration.Error;
        return Fs50Calibration
            .Parse(descriptor.Value.AsSpan(), controls.Value.AsSpan(), calibration.Value.AsSpan())
            .Bind(Fs50GeometryMap.Create)
            .Map(map => new Fs50FrameFactory(
                map,
                Fs50FrameFactory.Digest(descriptor.Value, controls.Value, calibration.Value),
                device
            ));
    }
}
