// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Concurrent;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>Driver discovery and process-wide exclusive physical-device leases.</summary>
public sealed class CaptureDriverRegistry
{
    private static readonly ConcurrentDictionary<string, byte> Leases = new(StringComparer.Ordinal);
    private readonly ImmutableArray<IFingerprintCaptureDriver> drivers;
    private readonly TimeProvider timeProvider;

    private CaptureDriverRegistry(
        ImmutableArray<IFingerprintCaptureDriver> drivers,
        TimeProvider timeProvider
    )
    {
        this.drivers = drivers;
        this.timeProvider = timeProvider;
    }

    /// <summary>Validates driver registrations without loading vendor libraries.</summary>
    /// <param name="drivers">Unique driver registrations.</param>
    /// <param name="timeProvider">Clock for finite session lifetimes.</param>
    /// <returns>A registry or invalid-argument failure.</returns>
    public static Result<CaptureDriverRegistry, CaptureError> Create(
        ImmutableArray<IFingerprintCaptureDriver> drivers,
        TimeProvider timeProvider
    )
    {
        if (
            drivers.IsDefault
            || drivers.Length > 64
            || timeProvider is null
            || drivers.Any(driver =>
                driver is null || !CaptureDeviceDescriptor.ValidText(driver.DriverId)
            )
        )
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Driver registrations are invalid."
            );
        if (
            drivers.Select(driver => driver.DriverId).Distinct(StringComparer.Ordinal).Count()
            != drivers.Length
        )
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Driver identifiers must be unique."
            );
        return new CaptureDriverRegistry(drivers, timeProvider);
    }

    /// <summary>Discovers devices and rejects conflicting physical identities.</summary>
    /// <param name="cancellationToken">Discovery cancellation.</param>
    /// <returns>A bounded immutable inventory or a typed failure.</returns>
    public async Task<Result<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>> DiscoverAsync(
        CancellationToken cancellationToken
    )
    {
        var devices = ImmutableArray.CreateBuilder<CaptureDeviceDescriptor>();
        foreach (var driver in drivers)
        {
            if (cancellationToken.IsCancellationRequested)
                return Cancelled<ImmutableArray<CaptureDeviceDescriptor>>();
            var result = await CaptureEdge
                .RunAsync(() => driver.DiscoverAsync(cancellationToken))
                .ConfigureAwait(false);
            if (result.IsFailure)
                return result.Error;
            if (
                result.Value.IsDefault
                || result.Value.Length + devices.Count > 256
                || result.Value.Any(device => device is null || device.DriverId != driver.DriverId)
            )
                return new CaptureError(
                    CaptureErrorCodes.InvalidArgument,
                    "The driver returned an invalid device inventory."
                );
            devices.AddRange(result.Value);
        }
        if (cancellationToken.IsCancellationRequested)
            return Cancelled<ImmutableArray<CaptureDeviceDescriptor>>();
        if (
            devices.Select(device => device.DeviceId).Distinct(StringComparer.Ordinal).Count()
            != devices.Count
        )
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Multiple drivers reported the same physical device."
            );
        return devices.ToImmutable();
    }

    /// <summary>Opens a discovered device with one lease shared by all registry instances.</summary>
    /// <param name="device">Descriptor obtained from discovery.</param>
    /// <param name="settings">Requested acquisition settings.</param>
    /// <param name="cancellationToken">Cancellation for opening and the complete session.</param>
    /// <returns>An owned finite session or a typed failure.</returns>
    public Task<Result<IFingerprintCaptureSession, CaptureError>> OpenAsync(
        CaptureDeviceDescriptor device,
        CaptureSettings settings,
        CancellationToken cancellationToken
    ) =>
        new CaptureSessionOpener(drivers, timeProvider, DiscoverAsync).OpenAsync(
            device,
            settings,
            cancellationToken
        );

    internal static bool TryLease(string deviceId) => Leases.TryAdd(deviceId, 0);

    internal static bool Release(string deviceId) => Leases.TryRemove(deviceId, out _);

    private static Result<T, CaptureError> Cancelled<T>() =>
        new CaptureError(CaptureErrorCodes.Cancelled, "Discovery was cancelled.");
}
