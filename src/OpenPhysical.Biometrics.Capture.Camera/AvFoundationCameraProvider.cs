// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Uses the ARC-managed macOS camera bridge with actual native frame layout validation.</summary>
public sealed class AvFoundationCameraProvider : ICameraProvider
{
    /// <inheritdoc/>
    public Task<Result<ImmutableArray<CameraSource>, CameraError>> EnumerateAsync(
        CancellationToken token
    ) =>
        CameraEdge.RunAsync(() =>
            Task.Run(
                () =>
                {
                    token.ThrowIfCancellationRequested();
                    return OperatingSystem.IsMacOS()
                        ? AvFoundationCatalog.Read()
                        : Result.Failure<ImmutableArray<CameraSource>, CameraError>(
                            AvFoundationStatus.Error(2)
                        );
                },
                token
            )
        );

    /// <inheritdoc/>
    public Task<Result<ICameraSession, CameraError>> OpenAsync(
        string sourceId,
        CameraMode mode,
        CancellationToken token
    ) =>
        CameraEdge.RunAsync(() =>
            Task.Run(
                () =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!OperatingSystem.IsMacOS() || mode.PixelFormat != "BGRA32")
                        return Result.Failure<ICameraSession, CameraError>(
                            AvFoundationStatus.Error(3)
                        );
                    var opened = AvFoundationCameraSession.Open(sourceId, mode);
                    if (opened.IsFailure)
                        return opened;
                    if (token.IsCancellationRequested)
                    {
                        opened.Value.DisposeAsync().AsTask().GetAwaiter().GetResult();
                        return Result.Failure<ICameraSession, CameraError>(
                            new(CameraErrorCodes.Cancelled, "Camera opening was cancelled.")
                        );
                    }
                    return opened;
                },
                token
            )
        );
}
