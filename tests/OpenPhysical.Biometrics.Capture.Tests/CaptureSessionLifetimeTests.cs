// SPDX-License-Identifier: GPL-2.0-or-later

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Capture.Tests;

/// <summary>Physical ownership and cleanup across cancellation, failures and device removal.</summary>
[TestFixture]
public sealed class CaptureSessionLifetimeTests
{
    /// <summary>Independent registries cannot concurrently acquire the same physical device.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task LeaseIsExclusiveAcrossRegistriesAndReleasedOnce()
    {
        var driver = new FakeCaptureDriver();
        var first = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        var duplicate = await driver
            .Registry()
            .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None);
        duplicate.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.DeviceBusy);
        await first.DisposeAsync();
        await first.DisposeAsync();
        driver.DisposeCount.Should().Be(1);
        var reopened = await driver
            .Registry()
            .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None);
        reopened.Should().Succeed();
        await reopened.Value.DisposeAsync();
    }

    /// <summary>Cancellation closes even an idle session and releases the physical lease.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task CancellationClosesIdleSession()
    {
        var driver = new FakeCaptureDriver();
        using var cancellation = new CancellationTokenSource();
        var session = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, cancellation.Token)
        ).Value;
        await cancellation.CancelAsync();
        await session.DisposeAsync();
        driver.DisposeCount.Should().Be(1);
        (await session.CaptureAsync(CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
        var reopened = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        await reopened.DisposeAsync();
    }

    /// <summary>In-flight operations serialize, and cancellation drains before reopening.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task CancelledCaptureReleasesOwnershipAfterCleanup()
    {
        var driver = new FakeCaptureDriver { BlockCapture = true };
        var session = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        using var cancellation = new CancellationTokenSource();
        var capture = session.CaptureAsync(cancellation.Token);
        await driver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        (await session.CaptureAsync(CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.DeviceBusy);
        await cancellation.CancelAsync();
        (await capture.WaitAsync(TimeSpan.FromSeconds(5)))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
        driver.DisposeCount.Should().Be(1);
        var reopened = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        await reopened.DisposeAsync();
    }

    /// <summary>A finite lifetime closes an otherwise idle session without caller disposal.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task LifetimeExpiryClosesSession()
    {
        var driver = new FakeCaptureDriver();
        var settings = CaptureSettings
            .Create(200, false, false, TimeSpan.FromMilliseconds(100))
            .Value;
        var session = (
            await driver.Registry().OpenAsync(driver.Device, settings, CancellationToken.None)
        ).Value;
        await driver.Closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.DisposeAsync();
        driver.DisposeCount.Should().Be(1);
        (await session.CaptureAsync(CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
    }

    /// <summary>Cancellation requested before an operation still closes and releases its session.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task PreCancelledOperationReleasesOwnership()
    {
        var driver = new FakeCaptureDriver();
        var session = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        (await session.CaptureAsync(new CancellationToken(true)))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
        driver.DisposeCount.Should().Be(1);
        var reopened = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        (await reopened.ApplySettingsAsync(FakeCaptureDriver.Settings, new CancellationToken(true)))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
        driver.DisposeCount.Should().Be(2);
    }
}
