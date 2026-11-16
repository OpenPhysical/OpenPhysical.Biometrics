// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using CSharpFunctionalExtensions;

/// <summary>Owns a running camera, one queued frame, and ordered reads.</summary>
public interface ICameraSession : IAsyncDisposable
{
    /// <summary>Reads the next available owned RGB frame.</summary>
    /// <param name="token">Read cancellation.</param>
    /// <returns>A frame the caller disposes, or a typed cancellation/disconnection failure.</returns>
    Task<Result<CameraFrame, CameraError>> ReadAsync(CancellationToken token);

    /// <summary>Fences late callbacks and drains native acquisition.</summary>
    /// <param name="token">Stop cancellation.</param>
    /// <returns>The stop outcome.</returns>
    Task<UnitResult<CameraError>> StopAsync(CancellationToken token);
}
