// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50CancellationTests
{
    [Test]
    public async Task IdleLifetimeCancellationReleasesHardwareWithoutAnotherCapture()
    {
        var usb = new FakeFs50UsbProvider();
        using var lifetime = new CancellationTokenSource();
        var opened = await OpenAsync(usb, lifetime.Token);
        await lifetime.CancelAsync();
        await usb.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        (await opened.CaptureAsync(CancellationToken.None))
            .Error.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
        usb.Closed.Should().Be(1);
    }

    [Test]
    public async Task ConcurrentSettingsAndCaptureFailBusyWithoutChangingAcquisition()
    {
        var usb = new FakeFs50UsbProvider { HoldCapture = true };
        await using var session = await OpenAsync(usb, CancellationToken.None);
        var capture = session.CaptureAsync(CancellationToken.None);
        await usb.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var settings = CaptureSettings.Create(20, false, false, TimeSpan.FromMinutes(1)).Value;
        (await session.ApplySettingsAsync(settings, CancellationToken.None))
            .Error.Code.Should()
            .Be(CaptureErrorCodes.DeviceBusy);
        (await session.CaptureAsync(CancellationToken.None))
            .Error.Code.Should()
            .Be(CaptureErrorCodes.DeviceBusy);
        usb.Release.TrySetResult();
        var frame = await capture;
        frame.Value.Provenance.Settings.Exposure.Should().Be(255);
        usb.Exposure.Should().Be(255);
    }

    [Test]
    public async Task OperationCancellationCannotReturnAFrameAndClosesTransport()
    {
        var usb = new FakeFs50UsbProvider { HoldCapture = true };
        await using var session = await OpenAsync(usb, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var capture = session.CaptureAsync(cancellation.Token);
        await usb.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();
        (await capture).Error.Code.Should().Be(CaptureErrorCodes.Cancelled);
        usb.Closed.Should().Be(1);
    }

    [Test]
    public async Task CancelledOpenDoesNotClaimHardware()
    {
        var usb = new FakeFs50UsbProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var settings = CaptureSettings.Create(255, false, false, TimeSpan.FromMinutes(1)).Value;
        var result = await new Fs50CaptureDriver(usb, TimeProvider.System).OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            cancellation.Token
        );
        result.Error.Code.Should().Be(CaptureErrorCodes.Cancelled);
        usb.Closed.Should().Be(0);
    }

    private static async Task<IFingerprintCaptureSession> OpenAsync(
        FakeFs50UsbProvider usb,
        CancellationToken token
    )
    {
        var settings = CaptureSettings.Create(255, false, false, TimeSpan.FromMinutes(1)).Value;
        var result = await new Fs50CaptureDriver(usb, TimeProvider.System).OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            token
        );
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }
}
