// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using FlashCap;

/// <summary>Uses the platform's camera backend without YUV matrix guesses or geometry resampling.</summary>
public sealed class FlashCapCameraProvider : ICameraProvider
{
    /// <inheritdoc/>
    public Task<Result<ImmutableArray<CameraSource>, CameraError>> EnumerateAsync(
        CancellationToken token
    ) =>
        CameraEdge.RunAsync(async () =>
        {
            var sources = await Task.Run(
                    () =>
                        new CaptureDevices()
                            .EnumerateDescriptors()
                            .Select(FlashCapModes.Describe)
                            .Where(value => !value.Modes.IsEmpty)
                            .ToImmutableArray(),
                    token
                )
                .ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return Result.Success<ImmutableArray<CameraSource>, CameraError>(sources);
        });

    /// <inheritdoc/>
    public Task<Result<ICameraSession, CameraError>> OpenAsync(
        string sourceId,
        CameraMode mode,
        CancellationToken token
    ) =>
        CameraEdge.RunAsync(async () =>
        {
            var descriptors = await Task.Run(
                    () => new CaptureDevices().EnumerateDescriptors().ToArray(),
                    token
                )
                .ConfigureAwait(false);
            var descriptor = descriptors
                .Where(value => FlashCapModes.Describe(value).Id == sourceId)
                .Select(Maybe.From)
                .FirstOrDefault();
            if (descriptor.HasNoValue)
                return new CameraError(
                    CameraErrorCodes.Unavailable,
                    "The selected camera is no longer connected."
                );
            return await OpenSelectedAsync(descriptor.Value, mode, token).ConfigureAwait(false);
        });

    private static async Task<Result<ICameraSession, CameraError>> OpenSelectedAsync(
        CaptureDeviceDescriptor descriptor,
        CameraMode mode,
        CancellationToken token
    )
    {
        var source = FlashCapModes.Describe(descriptor);
        var characteristic = descriptor
            .Characteristics.Where(value =>
                FlashCapModes.Describe(value) == mode
                && CameraBufferDecoder.Supported(mode.PixelFormat)
            )
            .Select(Maybe.From)
            .FirstOrDefault();
        if (characteristic.HasNoValue)
            return new CameraError(
                CameraErrorCodes.UnsupportedMode,
                "Select one of the camera's advertised RGB modes."
            );
        var session = new FlashCapCameraSession(
            source,
            mode,
            descriptor.DeviceType == DeviceTypes.AVFoundation
        );
        var opened = await session
            .OpenAsync(descriptor, characteristic.Value, token)
            .ConfigureAwait(false);
        return opened.IsSuccess
            ? Result.Success<ICameraSession, CameraError>(session)
            : opened.Error;
    }
}
