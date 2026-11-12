// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

internal static class PresenceFrameFixture
{
    private static readonly FakeCaptureDriver Driver = new();

    internal static CaptureFrame Create(
        byte[] pixels,
        int width = 2,
        int height = 2,
        int exposure = 200
    )
    {
        var geometry = CaptureGeometry.Create(width, height).Value;
        var settings = CaptureSettings
            .Create(exposure, false, false, TimeSpan.FromSeconds(30))
            .Value;
        var provenance = CaptureProvenance
            .Create(
                Driver.Device,
                "1",
                geometry,
                geometry,
                settings,
                [],
                string.Empty,
                CaptureResolution.Unknown,
                CaptureQualification.Experimental
            )
            .Value;
        return CaptureFrame.Create(pixels, 0, DateTimeOffset.UnixEpoch, provenance).Value;
    }
}
