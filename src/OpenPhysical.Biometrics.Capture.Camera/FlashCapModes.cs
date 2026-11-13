// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Collections.Immutable;
using FlashCap;

internal static class FlashCapModes
{
    internal static CameraMode Describe(VideoCharacteristics value) =>
        new(
            value.Width,
            value.Height,
            value.FramesPerSecond.Numerator,
            value.FramesPerSecond.Denominator,
            value.PixelFormat.ToString()
        );

    internal static CameraSource Describe(CaptureDeviceDescriptor descriptor) =>
        new(
            descriptor.Identity.ToString() ?? descriptor.Name,
            descriptor.Description,
            descriptor
                .Characteristics.Where(value =>
                    CameraBufferDecoder.Supported(value.PixelFormat.ToString())
                )
                .Select(Describe)
                .Distinct()
                .ToImmutableArray()
        );
}
