// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Discovers native cameras and opens a single selected mode.</summary>
public interface ICameraProvider
{
    /// <summary>Discovers cameras and native modes without acquiring image frames.</summary>
    /// <param name="token">Operation cancellation.</param>
    /// <returns>Discovered devices or a safe provider failure.</returns>
    Task<Result<ImmutableArray<CameraSource>, CameraError>> EnumerateAsync(CancellationToken token);

    /// <summary>Opens and starts a selected native mode.</summary>
    /// <param name="sourceId">Discovered camera identity.</param>
    /// <param name="mode">An advertised mode.</param>
    /// <param name="token">Operation cancellation.</param>
    /// <returns>An owned running session or an acquisition failure.</returns>
    Task<Result<ICameraSession, CameraError>> OpenAsync(
        string sourceId,
        CameraMode mode,
        CancellationToken token
    );
}
