// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;

internal sealed class ReenumeratingTestCameraProvider(ICameraProvider camera) : ICameraProvider
{
    public async Task<Result<ImmutableArray<CameraSource>, CameraError>> EnumerateAsync(
        CancellationToken token
    ) =>
        (await camera.EnumerateAsync(token)).Map(sources =>
            sources
                .Select(source =>
                    source with
                    {
                        Modes = [.. source.Modes.Select(mode => mode with { })],
                    }
                )
                .ToImmutableArray()
        );

    public Task<Result<ICameraSession, CameraError>> OpenAsync(
        string sourceId,
        CameraMode mode,
        CancellationToken token
    ) => camera.OpenAsync(sourceId, mode, token);
}
