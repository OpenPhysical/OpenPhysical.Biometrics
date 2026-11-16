// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

// Keeps one lifetime across discovery, opening and acquisition.
internal sealed class CaptureSessionOpener(
    ImmutableArray<IFingerprintCaptureDriver> drivers,
    TimeProvider timeProvider,
    Func<
        CancellationToken,
        Task<Result<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>>
    > discover
)
{
    internal async Task<Result<IFingerprintCaptureSession, CaptureError>> OpenAsync(
        CaptureDeviceDescriptor device,
        CaptureSettings settings,
        CancellationToken cancellationToken
    )
    {
        if (device is null || settings is null)
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Device and settings are required."
            );
        var timeout = new CancellationTokenSource(settings.Lifetime, timeProvider);
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token,
            cancellationToken
        );
        bool transferred = false;
        try
        {
            var result = await OpenWithinLifetimeAsync(device, settings, lifetime, timeout)
                .ConfigureAwait(false);
            transferred = result.IsSuccess;
            return result;
        }
        finally
        {
            if (!transferred)
            {
                lifetime.Dispose();
                timeout.Dispose();
            }
        }
    }

    private async Task<Result<IFingerprintCaptureSession, CaptureError>> OpenWithinLifetimeAsync(
        CaptureDeviceDescriptor device,
        CaptureSettings settings,
        CancellationTokenSource lifetime,
        CancellationTokenSource timeout
    )
    {
        var inventory = await discover(lifetime.Token).ConfigureAwait(false);
        if (inventory.IsFailure)
            return inventory.Error;
        if (!inventory.Value.Contains(device))
            return new CaptureError(
                CaptureErrorCodes.DeviceUnavailable,
                "The device descriptor is no longer current."
            );
        var supported = device.Capabilities.Validate(settings);
        if (supported.IsFailure)
            return supported.Error;
        if (!CaptureDriverRegistry.TryLease(device.DeviceId))
            return new CaptureError(
                CaptureErrorCodes.DeviceBusy,
                "Another session owns this device."
            );
        var driver = drivers.First(item => item.DriverId == device.DriverId);
        var result = await CaptureEdge
            .RunAsync(() => driver.OpenAsync(device.DeviceId, settings, lifetime.Token))
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            CaptureDriverRegistry.Release(device.DeviceId);
            return result.Error;
        }
        return await BindSessionAsync(result.Value, device, settings, lifetime, timeout)
            .ConfigureAwait(false);
    }

    private static async Task<Result<IFingerprintCaptureSession, CaptureError>> BindSessionAsync(
        IFingerprintCaptureSession inner,
        CaptureDeviceDescriptor device,
        CaptureSettings settings,
        CancellationTokenSource lifetime,
        CancellationTokenSource timeout
    )
    {
        // A malformed success gives us no handle whose closure can prove the device safe to reuse.
        if (inner is null)
            return await CancelMalformedSessionAsync(lifetime).ConfigureAwait(false);
        var session = new OwnedCaptureSession(inner, device, settings, lifetime, timeout);
        if (lifetime.IsCancellationRequested || inner.DeviceId != device.DeviceId)
        {
            var code = lifetime.IsCancellationRequested
                ? CaptureErrorCodes.Cancelled
                : CaptureErrorCodes.InvalidFrame;
            await session.DisposeAsync().ConfigureAwait(false);
            return new CaptureError(
                code,
                "The opening session was cancelled or returned the wrong device."
            );
        }
        session.ObserveCancellation();
        return Result.Success<IFingerprintCaptureSession, CaptureError>(session);
    }

    private static async Task<
        Result<IFingerprintCaptureSession, CaptureError>
    > CancelMalformedSessionAsync(CancellationTokenSource lifetime)
    {
        await CaptureEdge
            .ApplyAsync(async () =>
            {
                await lifetime.CancelAsync().ConfigureAwait(false);
                return UnitResult.Success<CaptureError>();
            })
            .ConfigureAwait(false);
        return new CaptureError(
            CaptureErrorCodes.InvalidFrame,
            "The driver returned no acquisition session."
        );
    }
}
