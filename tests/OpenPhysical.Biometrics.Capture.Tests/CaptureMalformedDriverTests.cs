// SPDX-License-Identifier: GPL-2.0-or-later

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Capture.Tests;

/// <summary>Malformed driver successes and discovery delays fail at the acquisition boundary.</summary>
[TestFixture]
public sealed class CaptureMalformedDriverTests
{
    /// <summary>No returned session handle means closure cannot be proven, so its lease is quarantined.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task NullOpenSuccessFailsAndQuarantinesLease()
    {
        var driver = new FakeCaptureDriver { NullOpen = true };
        var opened = await driver
            .Registry()
            .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None);
        opened.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.InvalidFrame);
        driver.NullOpen = false;
        var reopened = await driver
            .Registry()
            .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None);
        reopened.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.DeviceBusy);
        driver.OpenCount.Should().Be(1);
        driver.OpenCancellationObserved.Should().BeTrue();
    }

    /// <summary>A null frame result closes the known session rather than throwing and leaking its lease.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task NullFrameSuccessFailsClosesAndAllowsReopen()
    {
        var driver = new FakeCaptureDriver { NullFrame = true };
        var session = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        (await session.CaptureAsync(CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidFrame);
        driver.DisposeCount.Should().Be(1);
        driver.NullFrame = false;
        var reopened = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        await reopened.DisposeAsync();
    }

    /// <summary>The total opening budget also cancels cooperative rediscovery before any native open.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task DiscoveryConsumesOpeningLifetime()
    {
        var driver = new FakeCaptureDriver { BlockDiscovery = true };
        var settings = CaptureSettings
            .Create(200, false, false, TimeSpan.FromMilliseconds(100))
            .Value;
        var opened = await driver
            .Registry()
            .OpenAsync(driver.Device, settings, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        opened.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.Cancelled);
        driver.OpenCount.Should().Be(0);
        driver.BlockDiscovery = false;
        var reopened = (
            await driver
                .Registry()
                .OpenAsync(driver.Device, FakeCaptureDriver.Settings, CancellationToken.None)
        ).Value;
        await reopened.DisposeAsync();
    }
}
