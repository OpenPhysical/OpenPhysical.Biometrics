// SPDX-License-Identifier: GPL-2.0-or-later

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureReconnectTests
{
    [Test]
    public async Task AttachingAScannerAfterStartupAutomaticallyStartsThePreview()
    {
        var driver = new ControlledCaptureDriver { Available = false };
        var ui = new ControlledPreviewDispatcher();
        await using var capture = new CaptureCoordinator(
            driver.Registry,
            ui,
            TimeProvider.System,
            NullLogger<CaptureCoordinator>.Instance
        );
        (await capture.StartAsync(CancellationToken.None)).Should().Succeed();
        _ = await driver.Discovered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        driver.Available = true;
        (await driver.NextAsync()).Complete().Should().BeTrue();
        ui.Render(await ui.NextAsync()).Should().Succeed();

        capture.HasDisplayedFrame.Should().BeTrue();
        driver.OpenCount.Should().Be(1);
    }

    [Test]
    public async Task ResumeAfterIdleExpiryOpensANewSessionAndPreservesTheFrozenFrame()
    {
        var driver = new ControlledCaptureDriver();
        var ui = new ControlledPreviewDispatcher();
        var clock = new ControlledCaptureClock();
        await using var capture = new CaptureCoordinator(
            driver.Registry,
            ui,
            clock,
            NullLogger<CaptureCoordinator>.Instance
        );
        await capture.StartAsync(CancellationToken.None);
        (await driver.NextAsync()).Complete();
        ui.Render(await ui.NextAsync());
        var original = ui.Displayed.Value;
        var inFlight = await driver.NextAsync();
        var freezing = capture.FreezeAsync();
        inFlight.Complete();
        var frozen = await freezing;
        frozen.Should().Succeed();
        _ = clock.Advance(TimeSpan.FromMinutes(6));

        (await capture.ResumeAsync(CancellationToken.None)).Should().Succeed();
        _ = await driver.NextAsync();

        frozen.Value.Should().BeSameAs(original);
        ui.Displayed.Value.Should().BeSameAs(original);
        driver.OpenCount.Should().Be(2);
        driver.DisposeCount.Should().Be(1);
    }

    [Test]
    public async Task RepeatedStartDoesNotOpenAnotherSessionAndConcurrentStopsReleaseOnce()
    {
        var driver = new ControlledCaptureDriver();
        var ui = new ControlledPreviewDispatcher();
        await using var capture = new CaptureCoordinator(
            driver.Registry,
            ui,
            TimeProvider.System,
            NullLogger<CaptureCoordinator>.Instance
        );
        await capture.StartAsync(CancellationToken.None);
        _ = await driver.NextAsync();

        (await capture.StartAsync(CancellationToken.None)).Should().Succeed();
        var stopped = await Task.WhenAll(capture.StopAsync(), capture.StopAsync());

        stopped.Should().OnlyContain(result => result.IsSuccess);
        driver.OpenCount.Should().Be(1);
        driver.DisposeCount.Should().Be(1);
    }
}
