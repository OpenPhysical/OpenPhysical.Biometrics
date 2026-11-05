// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50CaptureSessionTests
{
    [Test]
    public async Task CorrectedCaptureBindsSettingsStageAndUnknownQualification()
    {
        var usb = new FakeFs50UsbProvider();
        var driver = new Fs50CaptureDriver(usb, TimeProvider.System);
        var settings = Settings(255, true);
        var opened = await driver.OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            CancellationToken.None
        );
        opened.IsSuccess.Should().BeTrue();
        await using var session = opened.Value;
        var captured = await session.CaptureAsync(CancellationToken.None);
        captured.IsSuccess.Should().BeTrue();
        captured.Value.Stage.Should().Be(CaptureFrame.FrameStage.Corrected);
        captured.Value.Pixels.All(x => x == 87).Should().BeTrue();
        captured.Value.Provenance.Settings.Should().Be(settings);
        captured.Value.Provenance.CalibrationDigest.Length.Should().Be(32);
        captured.Value.Provenance.Resolution.Should().Be(CaptureResolution.Unknown);
        captured.Value.Provenance.Qualification.IsQualified.Should().BeFalse();
        usb.Exposure.Should().Be(255);
    }

    [Test]
    public async Task RawCaptureAndSettingChangesRemainTruthful()
    {
        var usb = new FakeFs50UsbProvider();
        var driver = new Fs50CaptureDriver(usb, TimeProvider.System);
        var opened = await driver.OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            Settings(0, false),
            CancellationToken.None
        );
        await using var session = opened.Value;
        var first = await session.CaptureAsync(CancellationToken.None);
        first.Value.Stage.Should().Be(CaptureFrame.FrameStage.RawSensor);
        first.Value.Pixels.All(x => x == 168).Should().BeTrue();
        first.Value.Provenance.CalibrationDigest.Should().BeEmpty();
        first.Value.Sequence.Should().Be(0);
        var next = Settings(255, true);
        (await session.ApplySettingsAsync(next, CancellationToken.None))
            .IsSuccess.Should()
            .BeTrue();
        var second = await session.CaptureAsync(CancellationToken.None);
        second.Value.Sequence.Should().Be(1);
        second.Value.Provenance.Settings.Should().Be(next);
        usb.Exposure.Should().Be(255);
    }

    [Test]
    public async Task UnsupportedGeometryClosesOpenedTransport()
    {
        var usb = new FakeFs50UsbProvider();
        usb.Descriptor[4] = 0;
        var opened = await new Fs50CaptureDriver(usb, TimeProvider.System).OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            Settings(255, true),
            CancellationToken.None
        );
        opened.Error.Code.Should().Be(CaptureErrorCodes.UnsupportedGeometry);
        usb.Closed.Should().Be(1);
    }

    [Test]
    public async Task FailedFrameClosesSessionAndCannotBeRetried()
    {
        var usb = new FakeFs50UsbProvider { FailCapture = true };
        var opened = await new Fs50CaptureDriver(usb, TimeProvider.System).OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            Settings(255, false),
            CancellationToken.None
        );
        var failed = await opened.Value.CaptureAsync(CancellationToken.None);
        failed.Error.Code.Should().Be(CaptureErrorCodes.TransferIncomplete);
        usb.Closed.Should().Be(1);
        (await opened.Value.CaptureAsync(CancellationToken.None))
            .Error.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
        await opened.Value.DisposeAsync();
        usb.Closed.Should().Be(1);
    }

    [Test]
    public async Task CancellationDuringTransferClosesExactlyOnce()
    {
        var usb = new FakeFs50UsbProvider { CancelCapture = true };
        var opened = await new Fs50CaptureDriver(usb, TimeProvider.System).OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            Settings(255, false),
            usb.Cancellation.Token
        );
        var result = await opened.Value.CaptureAsync(CancellationToken.None);
        result.Error.Code.Should().Be(CaptureErrorCodes.Cancelled);
        await opened.Value.DisposeAsync();
        usb.Closed.Should().Be(1);
    }

    [Test]
    public async Task RegistryAcceptsDriverProvenanceAndReleasesLease()
    {
        var usb = new FakeFs50UsbProvider();
        var driver = new Fs50CaptureDriver(usb, TimeProvider.System);
        var registry = CaptureDriverRegistry.Create([driver], TimeProvider.System).Value;
        var devices = await registry.DiscoverAsync(CancellationToken.None);
        var opened = await registry.OpenAsync(
            devices.Value[0],
            Settings(255, false),
            CancellationToken.None
        );
        (await opened.Value.CaptureAsync(CancellationToken.None)).IsSuccess.Should().BeTrue();
        await opened.Value.DisposeAsync();
        usb.Closed.Should().Be(1);
    }

    [Test]
    public async Task CorrectionDigestBindsCropAndDescriptorAsWellAsCalibration()
    {
        var baseline = await CorrectionDigestAsync(new FakeFs50UsbProvider());
        var crop = new FakeFs50UsbProvider();
        crop.Controls[3]++;
        var descriptor = new FakeFs50UsbProvider();
        descriptor.Descriptor[20]++;
        (await CorrectionDigestAsync(crop)).Should().NotEqual(baseline);
        (await CorrectionDigestAsync(descriptor)).Should().NotEqual(baseline);
    }

    private static async Task<byte[]> CorrectionDigestAsync(FakeFs50UsbProvider usb)
    {
        var opened = await new Fs50CaptureDriver(usb, TimeProvider.System).OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            Settings(255, true),
            CancellationToken.None
        );
        opened.IsSuccess.Should().BeTrue();
        await using var session = opened.Value;
        var frame = await session.CaptureAsync(CancellationToken.None);
        return frame.Value.Provenance.CalibrationDigest.ToArray();
    }

    private static CaptureSettings Settings(int exposure, bool corrected) =>
        CaptureSettings.Create(exposure, false, corrected, TimeSpan.FromMinutes(1)).Value;
}
