// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class ControlledCaptureRequest(
    CaptureDeviceDescriptor device,
    CaptureSettings settings,
    long sequence,
    CancellationToken token
)
{
    private readonly TaskCompletionSource<Result<CaptureFrame, CaptureError>> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    internal CancellationToken Token => token;

    internal CaptureSettings Settings => settings;

    internal Task<Result<CaptureFrame, CaptureError>> Task => completion.Task;

    internal bool Complete() => Complete(new byte[800 * 750]);

    internal bool Complete(byte[] pixels)
    {
        var geometry = CaptureGeometry.Create(800, 750).Value;
        var provenance = CaptureProvenance
            .Create(
                device,
                "fixture",
                geometry,
                geometry,
                settings,
                new byte[32],
                "fixture",
                CaptureResolution.Unknown,
                CaptureQualification.Experimental
            )
            .Value;
        return completion.TrySetResult(
            CaptureFrame.Create(pixels, sequence, DateTimeOffset.UtcNow, provenance)
        );
    }

    internal bool Fail() =>
        completion.TrySetResult(
            new CaptureError(CaptureErrorCodes.DeviceUnavailable, "Device disconnected.")
        );

    internal bool Cancel() =>
        completion.TrySetResult(new CaptureError(CaptureErrorCodes.Cancelled, "Cancelled."));
}
