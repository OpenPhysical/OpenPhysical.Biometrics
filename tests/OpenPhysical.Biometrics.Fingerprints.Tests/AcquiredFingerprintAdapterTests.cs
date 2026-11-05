// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using static OpenPhysical.Biometrics.Fingerprints.Tests.AcquisitionFixtures;

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
}
