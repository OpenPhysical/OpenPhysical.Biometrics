// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal static class StorageFixtures
{
    internal static CapturedFingerprint Capture(
        FingerPosition finger = FingerPosition.RightIndex,
        int width = 8,
        int height = 8
    )
    {
        var frame = Frame(width, height);
        var view = new FingerprintView(
            (byte)finger,
            0,
            0,
            80,
            [new FingerprintMinutia(1, 2, 3, 45, 90)],
            []
        );
        return new CapturedFingerprint(
            frame,
            finger,
            view,
            FingerprintQualityAssessment.Create(2, "fixture-nfiq").Value
        );
    }

    private static CaptureFrame Frame(int width, int height)
    {
        var capabilities = CaptureCapabilities.Create(0, 255, true, true).Value;
        var device = CaptureDeviceDescriptor
            .Create("fixture-device", "futronic", "FS50", Maybe<string>.None, capabilities)
            .Value;
        var geometry = CaptureGeometry.Create(width, height).Value;
        var settings = CaptureSettings.Create(255, false, false, TimeSpan.FromSeconds(20)).Value;
        var resolution = CaptureResolution
            .Create(CaptureResolution.EvidenceKind.Declared, 500, 500, "Manufacturer")
            .Value;
        var provenance = CaptureProvenance
            .Create(
                device,
                "driver-v1",
                geometry,
                geometry,
                settings,
                [],
                string.Empty,
                resolution,
                CaptureQualification.Experimental
            )
            .Value;
        var pixels = Enumerable.Range(0, width * height).Select(value => (byte)value).ToArray();
        return CaptureFrame
            .Create(pixels, 42, DateTimeOffset.UnixEpoch.AddDays(1), provenance)
            .Value;
    }
}
