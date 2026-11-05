// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;

[TestFixture]
internal sealed class AcquiredFingerprintAdapterTests
{
    [Test]
    public void QualifiedCapturePreservesPixelsAndExplicitEquipmentMapping()
    {
        var frame = Frame();
        var result = new AcquiredFingerprintAdapter().Adapt(
            frame,
            FingerPosition.RightIndex,
            Profile()
        );
        result.Should().Succeed();
        result.Value.Image.Pixels.ToArray().Should().Equal(frame.Pixels);
        result.Value.EquipmentId.Should().Be(3);
        result.Value.CapturedAt.Should().Be(frame.CapturedAt);
    }

    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public void RawExperimentalAndRolledFramesCannotBecomeQualifiedPlainCaptures(
        bool corrected,
        bool qualified,
        bool rolled
    )
    {
        new AcquiredFingerprintAdapter()
            .Adapt(Frame(corrected, qualified, rolled), FingerPosition.RightIndex, Profile())
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.AcquisitionProfileMismatch);
    }

    [Test]
    public void CalibrationAndResolutionMustMatchTheQualifiedProcessingPath()
    {
        new AcquiredFingerprintAdapter()
            .Adapt(Frame(ppi: 1000), FingerPosition.RightIndex, Profile())
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.UnsupportedResolution);
        new AcquiredFingerprintAdapter()
            .Adapt(Frame(), FingerPosition.RightIndex, Profile(1))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.AcquisitionProfileMismatch);
    }

    [Test]
    public void UnknownEquipmentIsNotAssignedARegistrationNumber()
    {
        FingerprintAcquisitionProfile
            .Create(
                "profile",
                "device",
                "driver-v1",
                "correction-v1",
                new byte[32],
                "measured-profile",
                0,
                false,
                CaptureGeometry.Create(128, 128).Value
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.InvalidEquipment);
    }

    private static FingerprintAcquisitionProfile Profile(byte digestValue = 0) =>
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

    private static CaptureFrame Frame(
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
