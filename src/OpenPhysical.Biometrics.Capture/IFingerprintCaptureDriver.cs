// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>A device driver boundary; implementations enforce OS-level exclusive access.</summary>
public interface IFingerprintCaptureDriver
{
    /// <summary>Gets the stable driver registration identifier.</summary>
    string DriverId { get; }

    /// <summary>Enumerates bounded immutable physical-device descriptors.</summary>
    /// <param name="cancellationToken">Cancellation for discovery.</param>
    /// <returns>Devices or a safe typed failure.</returns>
    Task<Result<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>> DiscoverAsync(
        CancellationToken cancellationToken
    );

    /// <summary>Opens one device. The token bounds opening and the complete session lifetime.</summary>
    /// <param name="deviceId">Canonical physical identity from discovery.</param>
    /// <param name="settings">Validated requested settings.</param>
    /// <param name="cancellationToken">Cancellation for opening and acquisition.</param>
    /// <returns>A disposable session or a safe typed failure.</returns>
    Task<Result<IFingerprintCaptureSession, CaptureError>> OpenAsync(
        string deviceId,
        CaptureSettings settings,
        CancellationToken cancellationToken
    );
}
