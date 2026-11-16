// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>A single-owner acquisition session with cancellation-aware operations.</summary>
public interface IFingerprintCaptureSession : IAsyncDisposable
{
    /// <summary>Gets the canonical physical device identity.</summary>
    string DeviceId { get; }

    /// <summary>Acquires one complete frame.</summary>
    /// <param name="cancellationToken">Cancellation for this acquisition.</param>
    /// <returns>A frame or a safe typed failure.</returns>
    Task<Result<CaptureFrame, CaptureError>> CaptureAsync(CancellationToken cancellationToken);

    /// <summary>Applies supported settings without extending the original session lifetime.</summary>
    /// <param name="settings">New settings.</param>
    /// <param name="cancellationToken">Cancellation for the change.</param>
    /// <returns>Success only after the device applies settings.</returns>
    Task<UnitResult<CaptureError>> ApplySettingsAsync(
        CaptureSettings settings,
        CancellationToken cancellationToken
    );
}
