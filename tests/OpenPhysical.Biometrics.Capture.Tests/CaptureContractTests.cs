// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Capture.Tests;

/// <summary>Validation of image bytes, physical-scale evidence and device capability contracts.</summary>
[TestFixture]
public sealed class CaptureContractTests
{
    /// <summary>Rejects impossible dimensions before allocation.</summary>
    /// <param name="width">Invalid width.</param>
    /// <param name="height">Invalid height.</param>
    [TestCase(0, 1)]
    [TestCase(1, -1)]
    [TestCase(int.MaxValue, int.MaxValue)]
    [TestCase(8192, 8192)]
    public void RejectsInvalidGeometry(int width, int height) =>
        CaptureGeometry
            .Create(width, height)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.UnsupportedGeometry);

    /// <summary>Prevents invented or invalid physical resolution.</summary>
    [Test]
    public void ResolutionRemainsUnknownUntilEvidenceExists()
    {
        CaptureResolution.Unknown.HorizontalPpi.HasValue.Should().BeFalse();
        CaptureResolution
            .Create(CaptureResolution.EvidenceKind.Measured, double.NaN, 500, "fixture")
            .Should()
            .Fail();
        CaptureResolution
            .Create(CaptureResolution.EvidenceKind.Unknown, 500, 500, "fixture")
            .Should()
            .Fail();
        CaptureResolution
            .Create(CaptureResolution.EvidenceKind.Measured, 500, 500, "")
            .Should()
            .Fail();
        CaptureResolution
            .Create(CaptureResolution.EvidenceKind.Declared, 510, 495, "datasheet")
            .Should()
            .Succeed()
            .Which.Kind.Should()
            .Be(CaptureResolution.EvidenceKind.Declared);
    }

    /// <summary>Owns image memory and derives raw stage only from consistent provenance.</summary>
    [Test]
    public void FrameCopiesBytesAndRejectsIncompleteTransfer()
    {
        var driver = new FakeCaptureDriver();
        var provenance = driver.Provenance(FakeCaptureDriver.Settings);
        var pixels = new byte[] { 1, 2, 3, 4 };
        var result = CaptureFrame.Create(pixels, 0, DateTimeOffset.UtcNow, provenance);
        pixels[0] = 99;
        result.Should().Succeed().Which.Pixels[0].Should().Be(1);
        result.Value.Stage.Should().Be(CaptureFrame.FrameStage.RawSensor);
        result.Value.Provenance.Resolution.Kind.Should().Be(CaptureResolution.EvidenceKind.Unknown);
        CaptureFrame
            .Create(pixels.AsSpan(0, 3), 0, DateTimeOffset.UtcNow, provenance)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.TransferIncomplete);
    }

    /// <summary>Raw acquisition cannot claim correction, qualification or changed geometry.</summary>
    [Test]
    public void RejectsInconsistentProvenance()
    {
        var driver = new FakeCaptureDriver();
        var geometry = CaptureGeometry.Create(2, 2).Value;
        driver
            .CheckProvenance(
                FakeCaptureDriver.Settings,
                geometry,
                new byte[32],
                "1",
                CaptureQualification.Experimental
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.CalibrationInvalid);
        driver
            .CheckProvenance(
                FakeCaptureDriver.Settings,
                CaptureGeometry.Create(3, 2).Value,
                [],
                "",
                CaptureQualification.Experimental
            )
            .Should()
            .Fail();
        driver
            .CheckProvenance(
                FakeCaptureDriver.Settings,
                geometry,
                [],
                "",
                CaptureQualification.Qualified("profile").Value
            )
            .Should()
            .Fail();
    }

    /// <summary>Corrected images require calibration evidence and preserve digest ownership.</summary>
    [Test]
    public void CorrectedImageRequiresCalibration()
    {
        var driver = new FakeCaptureDriver();
        var geometry = CaptureGeometry.Create(2, 2).Value;
        var settings = CaptureSettings.Create(200, false, true, TimeSpan.FromSeconds(30)).Value;
        driver
            .CheckProvenance(settings, geometry, [], "", CaptureQualification.Experimental)
            .Should()
            .Fail();
        var digest = new byte[32];
        var provenance = driver
            .CheckProvenance(
                settings,
                geometry,
                digest,
                "correction1",
                CaptureQualification.Experimental
            )
            .Value;
        digest[0] = 1;
        provenance.CalibrationDigest[0].Should().Be(0);
        CaptureFrame
            .Create(new byte[4], 0, DateTimeOffset.UtcNow, provenance)
            .Value.Stage.Should()
            .Be(CaptureFrame.FrameStage.Corrected);
    }

    /// <summary>Invalid lifetime, unsupported settings and duplicate registrations fail explicitly.</summary>
    [Test]
    public void RejectsUnsupportedSettingsAndRegistrations()
    {
        var driver = new FakeCaptureDriver();
        CaptureSettings.Create(256, false, false, TimeSpan.FromSeconds(1)).Should().Fail();
        CaptureSettings.Create(1, false, false, TimeSpan.Zero).Should().Fail();
        CaptureSettings.Create(1, false, false, TimeSpan.FromHours(1)).Should().Fail();
        driver
            .Device.Capabilities.Validate(
                CaptureSettings.Create(1, true, false, TimeSpan.FromSeconds(1)).Value
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.UnsupportedCapability);
        CaptureDriverRegistry
            .Create(
                ImmutableArray.Create<IFingerprintCaptureDriver>(driver, driver),
                TimeProvider.System
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
    }
}
