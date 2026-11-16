// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using CSharpFunctionalExtensions;

internal static class Fs50EvidenceFrameFixture
{
    internal static CaptureFrame Create(
        byte[] pixels,
        long sequence,
        int exposure = 200,
        byte calibration = 0
    )
    {
        var device = CaptureDeviceDescriptor
            .Create(
                "synthetic-fs50",
                "futronic.fs50",
                "FS50",
                Maybe<string>.None,
                CaptureCapabilities.Create(0, 255, false, true).Value
            )
            .Value;
        var digest = Enumerable.Repeat(calibration, 32).ToArray();
        var settings = CaptureSettings.Create(exposure, false, true, TimeSpan.FromMinutes(5)).Value;
        var provenance = CaptureProvenance
            .Create(
                device,
                "test",
                CaptureGeometry.Create(1280, 832).Value,
                CaptureGeometry.Create(800, 750).Value,
                settings,
                digest,
                "synthetic-correction",
                CaptureResolution.Unknown,
                CaptureQualification.Experimental
            )
            .Value;
        return CaptureFrame
            .Create(
                pixels,
                sequence,
                DateTimeOffset.UnixEpoch.AddMilliseconds(sequence),
                provenance
            )
            .Value;
    }

    internal static byte[] Empty() => Enumerable.Repeat((byte)255, 800 * 750).ToArray();
}
