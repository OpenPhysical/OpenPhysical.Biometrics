// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

namespace OpenPhysical.Middleware.Tests;

internal sealed class FingerprintCaptureTestDriver(bool qualified) : IFingerprintCaptureDriver
{
    public string DriverId => "fixture";
    public CaptureDeviceDescriptor Device { get; } =
        CaptureDeviceDescriptor
            .Create(
                Guid.NewGuid().ToString(),
                "fixture",
                "fixture",
                Maybe<string>.None,
                CaptureCapabilities.Create(1, 255, false, true).Value
            )
            .Value;
    internal int Opened { get; private set; }
    internal int Disposed { get; private set; }
    internal Action OnCapture { get; set; } = () => { };
    internal bool CancelCapture { get; set; }

    public Task<Result<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>> DiscoverAsync(
        CancellationToken token
    ) =>
        Task.FromResult(
            Result.Success<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>([Device])
        );

    public Task<Result<IFingerprintCaptureSession, CaptureError>> OpenAsync(
        string deviceId,
        CaptureSettings settings,
        CancellationToken token
    )
    {
        Opened++;
        return Task.FromResult(
            Result.Success<IFingerprintCaptureSession, CaptureError>(
                new Session(this, settings, qualified)
            )
        );
    }

    private sealed class Session(
        FingerprintCaptureTestDriver driver,
        CaptureSettings settings,
        bool qualified
    ) : IFingerprintCaptureSession
    {
        public string DeviceId => driver.Device.DeviceId;

        public Task<Result<CaptureFrame, CaptureError>> CaptureAsync(CancellationToken token)
        {
            driver.OnCapture();
            if (driver.CancelCapture)
                return Task.FromResult(
                    Result.Failure<CaptureFrame, CaptureError>(
                        new(CaptureErrorCodes.Cancelled, "fixture cancellation")
                    )
                );
            var geometry = CaptureGeometry.Create(128, 128).Value;
            var provenance = CaptureProvenance
                .Create(
                    driver.Device,
                    "1",
                    geometry,
                    geometry,
                    settings,
                    new byte[32],
                    "1",
                    CaptureResolution
                        .Create(CaptureResolution.EvidenceKind.Measured, 500, 500, "fixture-only")
                        .Value,
                    qualified
                        ? CaptureQualification.Qualified("fixture-only").Value
                        : CaptureQualification.Experimental
                )
                .Value;
            return Task.FromResult(
                CaptureFrame.Create(
                    new byte[128 * 128],
                    0,
                    VerificationBundleFixture.Now,
                    provenance
                )
            );
        }

        public Task<UnitResult<CaptureError>> ApplySettingsAsync(
            CaptureSettings value,
            CancellationToken token
        ) => Task.FromResult(UnitResult.Success<CaptureError>());

        public ValueTask DisposeAsync()
        {
            driver.Disposed++;
            return ValueTask.CompletedTask;
        }
    }
}
