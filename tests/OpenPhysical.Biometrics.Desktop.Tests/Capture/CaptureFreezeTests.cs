// SPDX-License-Identifier: GPL-2.0-or-later

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureFreezeTests
{
    [Test]
    public async Task CaptureFreezesDisplayedFrameAndDrainsWithoutCancellingTheNewerFrame()
    {
        var driver = new ControlledCaptureDriver();
        var ui = new ControlledPreviewDispatcher();
        await using var capture = NewCoordinator(driver, ui);
        (await capture.StartAsync(CancellationToken.None)).Should().Succeed();
        (await driver.NextAsync()).Complete().Should().BeTrue();
        ui.Render(await ui.NextAsync()).Should().Succeed();
        var displayed = ui.Displayed.Value;
        var inFlight = await driver.NextAsync();

        var freezing = capture.FreezeAsync();

        freezing.IsCompleted.Should().BeFalse();
        inFlight.Token.IsCancellationRequested.Should().BeFalse();
        inFlight.Complete().Should().BeTrue();
        (await freezing).Should().Succeed().Which.Should().BeSameAs(displayed);
        driver.OpenCount.Should().Be(1);
        driver.DisposeCount.Should().Be(0);
    }

    [Test]
    public async Task QueuedPreviewCannotReplaceFrozenImageOrAnImageFromTheNextLiveGeneration()
    {
        var driver = new ControlledCaptureDriver();
        var ui = new ControlledPreviewDispatcher();
        await using var capture = NewCoordinator(driver, ui);
        await capture.StartAsync(CancellationToken.None);
        (await driver.NextAsync()).Complete();
        ui.Render(await ui.NextAsync());
        var original = ui.Displayed.Value;
        (await driver.NextAsync()).Complete();
        var queued = await ui.NextAsync();
        var pending = await driver.NextAsync();
        var frozen = capture.FreezeAsync();
        pending.Complete();
        (await frozen).Should().Succeed();
        ui.Render(queued).Should().Succeed();
        ui.Displayed.Value.Should().BeSameAs(original);

        (await capture.ResumeAsync(CancellationToken.None)).Should().Succeed();
        var next = await driver.NextAsync();
        ui.Render(queued).Should().Succeed();
        ui.Displayed.Value.Should().BeSameAs(original);
        next.Complete();
        ui.Render(await ui.NextAsync()).Should().Succeed();
        ui.Displayed.Value.Should().NotBeSameAs(original);
    }

    [Test]
    public async Task DisconnectWhileDrainingPreservesTheFrozenCaptureAndItsOriginalSettings()
    {
        var driver = new ControlledCaptureDriver();
        var ui = new ControlledPreviewDispatcher();
        await using var capture = NewCoordinator(driver, ui);
        await capture.SetExposureAsync(200, false);
        await capture.StartAsync(CancellationToken.None);
        (await driver.NextAsync()).Complete();
        ui.Render(await ui.NextAsync());
        var pending = await driver.NextAsync();
        (await capture.SetExposureAsync(125, false)).Should().Succeed();

        var frozen = capture.FreezeAsync();
        pending.Fail();

        var result = await frozen;
        result.Should().Succeed();
        result.Value.Provenance.Settings.Exposure.Should().Be(200);
        result.Value.Should().BeSameAs(ui.Displayed.Value);
    }

    [Test]
    public async Task CaptureWithoutADisplayedFrameFailsAndShutdownCancelsThePendingAcquisition()
    {
        var driver = new ControlledCaptureDriver();
        var ui = new ControlledPreviewDispatcher();
        await using var capture = NewCoordinator(driver, ui);
        await capture.StartAsync(CancellationToken.None);
        var pending = await driver.NextAsync();

        (await capture.FreezeAsync()).Should().Fail().Which.Code.Should().Be("Capture.NoFrame");
        (await capture.StopAsync()).Should().Succeed();

        pending.Token.IsCancellationRequested.Should().BeTrue();
        driver.DisposeCount.Should().Be(1);
    }

    [Test]
    public async Task FailedRenderingCannotMakeAnImageEligibleForCapture()
    {
        var driver = new ControlledCaptureDriver();
        var ui = new ControlledPreviewDispatcher();
        await using var capture = NewCoordinator(driver, ui);
        await capture.StartAsync(CancellationToken.None);
        (await driver.NextAsync()).Complete();
        var update = await ui.NextAsync();
        update
            .Display(_ =>
                CSharpFunctionalExtensions.UnitResult.Failure(
                    new DesktopError("Test.Render", "Unavailable")
                )
            )
            .Should()
            .Fail();
        update.Acknowledged.HasNoValue.Should().BeTrue();
        capture.HasDisplayedFrame.Should().BeFalse();
        (await capture.FreezeAsync()).Should().Fail().Which.Code.Should().Be("Capture.NoFrame");
    }

    private static CaptureCoordinator NewCoordinator(
        ControlledCaptureDriver driver,
        ControlledPreviewDispatcher ui
    ) => new(driver.Registry, ui, TimeProvider.System, NullLogger<CaptureCoordinator>.Instance);
}
