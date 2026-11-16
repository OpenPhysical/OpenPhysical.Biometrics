// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal static class AcquisitionFixtures
{
    internal static FingerprintAcquisitionProfile Profile(byte digestValue = 0) =>
        FingerprintAcquisitionProfile
            .Create(
                "profile",
                "device",
                "driver-v1",
                "correction-v1",
                Enumerable.Repeat(digestValue, 32).ToArray(),
                "measured-profile",
                3,
                true,
                CaptureGeometry.Create(128, 128).Value
            )
            .Value;

    internal static CaptureFrame Frame(
        bool corrected = true,
        bool qualified = true,
        bool rolled = false,
        double ppi = 500
    )
    {
        var capabilities = CaptureCapabilities.Create(0, 255, true, true).Value;
        var device = CaptureDeviceDescriptor
            .Create("device", "futronic", "test model", Maybe<string>.None, capabilities)
            .Value;
        var geometry = CaptureGeometry.Create(128, 128).Value;
        var settings = CaptureSettings
            .Create(255, rolled, corrected, TimeSpan.FromSeconds(20))
            .Value;
        var resolution = CaptureResolution
            .Create(CaptureResolution.EvidenceKind.Measured, ppi, ppi, "measurement")
            .Value;
        var qualification = qualified
            ? CaptureQualification.Qualified("measured-profile").Value
            : CaptureQualification.Experimental;
        var provenance = CaptureProvenance
            .Create(
                device,
                "driver-v1",
                geometry,
                geometry,
                settings,
                corrected ? new byte[32] : Array.Empty<byte>(),
                corrected ? "correction-v1" : string.Empty,
                resolution,
                qualification
            )
            .Value;
        return CaptureFrame
            .Create(new byte[128 * 128], 0, DateTimeOffset.UnixEpoch, provenance)
            .Value;
    }
}
