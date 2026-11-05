// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture.Tests;

internal sealed class FakeCaptureDriver : IFingerprintCaptureDriver
{
    internal FakeCaptureDriver()
    {
        Device = CaptureDeviceDescriptor
            .Create(
                Guid.NewGuid().ToString(),
                DriverId,
                "fixture",
                Maybe<string>.None,
                CaptureCapabilities.Create(1, 255, false, true).Value
            )
            .Value;
    }

    public string DriverId => "fixture";
    internal CaptureDeviceDescriptor Device { get; }
    internal int DisposeCount { get; private set; }
    internal int OpenCount { get; private set; }
    internal bool BlockCapture { get; set; }
    internal bool BlockDiscovery { get; set; }
    internal bool NullOpen { get; set; }
    internal bool NullFrame { get; set; }
    internal bool OpenCancellationObserved { get; private set; }
    internal bool Disconnect { get; set; }
    internal bool RepeatSequence { get; set; }
    internal bool FailOpen { get; set; }
    internal bool ThrowOnOpen { get; set; }
    internal bool FailClose { get; set; }
    internal bool WrongDevice { get; set; }
    internal TaskCompletionSource<bool> Entered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<bool> Closed { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal static CaptureSettings Settings =>
        CaptureSettings.Create(200, false, false, TimeSpan.FromSeconds(30)).Value;

    internal CaptureDriverRegistry Registry() =>
        CaptureDriverRegistry
            .Create(ImmutableArray.Create<IFingerprintCaptureDriver>(this), TimeProvider.System)
            .Value;

    public async Task<Result<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>> DiscoverAsync(
        CancellationToken cancellationToken
    )
    {
        if (BlockDiscovery)
        {
            var cancelled = new TaskCompletionSource<
                Result<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>
            >(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() =>
                cancelled.TrySetResult(
                    new CaptureError(CaptureErrorCodes.Cancelled, "fixture discovery cancelled")
                )
            );
            return await cancelled.Task;
        }
        return ImmutableArray.Create(Device);
    }

    public Task<Result<IFingerprintCaptureSession, CaptureError>> OpenAsync(
        string deviceId,
        CaptureSettings settings,
        CancellationToken cancellationToken
    )
    {
        OpenCount++;
        if (NullOpen)
        {
            cancellationToken.Register(() => OpenCancellationObserved = true);
            return Task.FromResult(default(Result<IFingerprintCaptureSession, CaptureError>));
        }
        if (ThrowOnOpen)
            return Task.FromException<Result<IFingerprintCaptureSession, CaptureError>>(
                new IOException("sensitive-driver-sentinel")
            );
        return Task.FromResult(
            FailOpen
                ? Result.Failure<IFingerprintCaptureSession, CaptureError>(
                    new(CaptureErrorCodes.DeviceUnavailable, "fixture open failure")
                )
                : Result.Success<IFingerprintCaptureSession, CaptureError>(
                    new Session(this, settings)
                )
        );
    }

    internal CaptureProvenance Provenance(CaptureSettings settings)
    {
        var geometry = CaptureGeometry.Create(2, 2).Value;
        return CaptureProvenance
            .Create(
                Device,
                "1",
                geometry,
                geometry,
                settings,
                settings.Corrected ? new byte[32] : Array.Empty<byte>(),
                settings.Corrected ? "1" : string.Empty,
                CaptureResolution.Unknown,
                CaptureQualification.Experimental
            )
            .Value;
    }

    internal Result<CaptureProvenance, CaptureError> CheckProvenance(
        CaptureSettings settings,
        CaptureGeometry output,
        ReadOnlySpan<byte> digest,
        string correction,
        CaptureQualification qualification
    ) =>
        CaptureProvenance.Create(
            Device,
            "1",
            CaptureGeometry.Create(2, 2).Value,
            output,
            settings,
            digest,
            correction,
            CaptureResolution.Unknown,
            qualification
        );

    private sealed class Session(FakeCaptureDriver driver, CaptureSettings settings)
        : IFingerprintCaptureSession
    {
        private long sequence;
        private CaptureSettings current = settings;
        public string DeviceId => driver.WrongDevice ? "different-device" : driver.Device.DeviceId;

        public async Task<Result<CaptureFrame, CaptureError>> CaptureAsync(
            CancellationToken cancellationToken
        )
        {
            driver.Entered.TrySetResult(true);
            if (driver.NullFrame)
                return default;
            if (driver.BlockCapture)
            {
                var cancelled = new TaskCompletionSource<Result<CaptureFrame, CaptureError>>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                using var registration = cancellationToken.Register(() =>
                    cancelled.TrySetResult(
                        new CaptureError(CaptureErrorCodes.Cancelled, "fixture cancellation")
                    )
                );
                return await cancelled.Task;
            }
            if (driver.Disconnect)
                return new CaptureError(CaptureErrorCodes.DeviceUnavailable, "fixture disconnect");
            return CaptureFrame.Create(
                new byte[4],
                driver.RepeatSequence ? 0 : sequence++,
                DateTimeOffset.UtcNow,
                driver.Provenance(current)
            );
        }

        public Task<UnitResult<CaptureError>> ApplySettingsAsync(
            CaptureSettings settings,
            CancellationToken cancellationToken
        )
        {
            current = settings;
            return Task.FromResult(UnitResult.Success<CaptureError>());
        }

        public ValueTask DisposeAsync()
        {
            driver.DisposeCount++;
            if (driver.FailClose)
                return new ValueTask(
                    Task.FromException(new IOException("sensitive-close-sentinel"))
                );
            driver.Closed.TrySetResult(true);
            return ValueTask.CompletedTask;
        }
    }
}
