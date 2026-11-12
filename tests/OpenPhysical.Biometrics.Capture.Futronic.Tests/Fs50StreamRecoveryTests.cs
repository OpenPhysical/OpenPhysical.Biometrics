// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50StreamRecoveryTests
{
    [Test]
    public async Task CancelledImageIsFullyDiscardedBeforeTheSessionClosesAndReopens()
    {
        using var cancellation = new CancellationTokenSource();
        var usb = new QueuedFs50UsbProvider { CancelOnImageRead = Maybe.From(cancellation) };
        var driver = new Fs50CaptureDriver(usb, TimeProvider.System);
        var settings = CaptureSettings.Create(220, false, true, TimeSpan.FromMinutes(5)).Value;
        var opened = await driver.OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            CancellationToken.None
        );
        opened.Should().Succeed();
        var cancelled = await opened.Value.CaptureAsync(cancellation.Token);
        cancelled.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.Cancelled);
        usb.ImageReads.Should().Be(17);
        usb.QueuedBytes.Should().Be(0);
        await opened.Value.DisposeAsync();
        var reopened = await driver.OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            CancellationToken.None
        );
        reopened.Should().Succeed();
        await reopened.Value.DisposeAsync();
    }

    [Test]
    public async Task CancellationBeforeFirstImageByteWaitsForTheKnownIssuedReply()
    {
        using var cancellation = new CancellationTokenSource();
        var usb = new QueuedFs50UsbProvider
        {
            CancelOnImageCommand = Maybe.From(cancellation),
            ImageReadTimeouts = 2,
        };
        var driver = new Fs50CaptureDriver(usb, TimeProvider.System);
        var settings = CaptureSettings.Create(220, false, true, TimeSpan.FromMinutes(5)).Value;
        var opened = await driver.OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            CancellationToken.None
        );
        opened.Should().Succeed();
        var cancelled = await opened.Value.CaptureAsync(cancellation.Token);
        cancelled.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.Cancelled);
        usb.ImageReads.Should().Be(17);
        usb.QueuedBytes.Should().Be(0);
        await opened.Value.DisposeAsync();
        var reopened = await driver.OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            CancellationToken.None
        );
        reopened.Should().Succeed();
        await reopened.Value.DisposeAsync();
    }

    [Test]
    public async Task CancelledReplyDrainsPartialReadsWithoutRelaxingNormalCaptureValidation()
    {
        using var cancellation = new CancellationTokenSource();
        var usb = new QueuedFs50UsbProvider
        {
            CancelOnImageCommand = Maybe.From(cancellation),
            ImageChunkLimit = 32768,
        };
        var driver = new Fs50CaptureDriver(usb, TimeProvider.System);
        var settings = CaptureSettings.Create(220, false, true, TimeSpan.FromMinutes(5)).Value;
        var opened = await driver.OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            CancellationToken.None
        );
        var captured = await opened.Value.CaptureAsync(cancellation.Token);
        captured.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.Cancelled);
        usb.ImageReads.Should().Be(33);
        usb.QueuedBytes.Should().Be(0);
        await opened.Value.DisposeAsync();
        usb.CancelOnImageCommand = Maybe<CancellationTokenSource>.None;
        var reopened = await driver.OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            CancellationToken.None
        );
        var shortFrame = await reopened.Value.CaptureAsync(CancellationToken.None);
        shortFrame.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.TransferIncomplete);
        await reopened.Value.DisposeAsync();
    }

    [Test]
    public async Task FreshOpenDiscardsStaleResponseBeforeSendingDescriptorOrCalibrationCommands()
    {
        var usb = new QueuedFs50UsbProvider();
        usb.InjectStale(1280 * 832);
        var driver = new Fs50CaptureDriver(usb, TimeProvider.System);
        var settings = CaptureSettings.Create(220, false, true, TimeSpan.FromMinutes(5)).Value;
        var opened = await driver.OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            CancellationToken.None
        );
        opened.Should().Succeed();
        usb.QueuedBytes.Should().Be(0);
        usb.Reads.Should().BeGreaterThan(138);
        await opened.Value.DisposeAsync();
    }

    [Test]
    public async Task EndpointThatNeverBecomesQuietFailsBoundedlyWithoutSendingAnyCommand()
    {
        var usb = new QueuedFs50UsbProvider { NeverQuiet = true };
        var driver = new Fs50CaptureDriver(usb, TimeProvider.System);
        var settings = CaptureSettings.Create(220, false, true, TimeSpan.FromMinutes(5)).Value;
        var opened = await driver.OpenAsync(
            FakeFs50UsbProvider.Device.DeviceId,
            settings,
            CancellationToken.None
        );
        opened.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.SynchronizationFailed);
        usb.Writes.Should().Be(0);
        usb.Reads.Should().BeLessThanOrEqualTo(65);
    }
}
