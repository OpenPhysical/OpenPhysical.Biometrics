// SPDX-License-Identifier: GPL-2.0-or-later

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Capture.Tests;

/// <summary>Device failure and frame substitution terminate acquisition safely.</summary>
[TestFixture]
public sealed class CaptureSessionFailureTests
{
    /// <summary>Disconnects and repeated frame sequence values terminate acquisition.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task InvalidFrameAndDisconnectCloseSession()
    {
        var driver = new FakeCaptureDriver { RepeatSequence = true };
        var session = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        (await session.CaptureAsync(CancellationToken.None)).Should().Succeed();
        (await session.CaptureAsync(CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidFrame);
        driver.DisposeCount.Should().Be(1);
        driver.Disconnect = true;
        var reopened = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        (await reopened.CaptureAsync(CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.DeviceUnavailable);
        driver.DisposeCount.Should().Be(2);
    }

    /// <summary>Open failures and wrong-device results do not leave an unowned successful session.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task OpenFailureAndDeviceSubstitutionAreRejected()
    {
        var driver = new FakeCaptureDriver { FailOpen = true };
        (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.DeviceUnavailable);
        driver.FailOpen = false;
        driver.WrongDevice = true;
        (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        )
            .Should()
            .Fail();
        driver.DisposeCount.Should().Be(1);
        driver.WrongDevice = false;
        var session = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        var updated = CaptureSettings.Create(255, false, false, TimeSpan.FromSeconds(30)).Value;
        (await session.ApplySettingsAsync(updated, CancellationToken.None)).Should().Succeed();
        (await session.CaptureAsync(CancellationToken.None))
            .Value.Provenance.Settings.Exposure.Should()
            .Be(255);
        await session.DisposeAsync();
    }

    /// <summary>Driver exception text never leaves the acquisition boundary.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task DriverExceptionsHaveSafeTypedErrors()
    {
        var driver = new FakeCaptureDriver { ThrowOnOpen = true };
        var failure = await driver
            .Registry()
            .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None);
        failure.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.DeviceUnavailable);
        failure.Error.Message.Should().NotContain("sentinel");
        driver.ThrowOnOpen = false;
        var session = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        await session.DisposeAsync();
    }

    /// <summary>A failed driver close cannot make the device available for overlapping I/O.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task FailedCloseQuarantinesPhysicalLease()
    {
        var driver = new FakeCaptureDriver { FailClose = true };
        var session = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        await session.DisposeAsync();
        (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.DeviceBusy);
        driver.OpenCount.Should().Be(1);
    }
}
