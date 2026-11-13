// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureAutoFreezeTests
{
    [AvaloniaTest]
    public async Task AutomaticCaptureAcknowledgesAndFreezesTheTriggeringFrameWithoutDeadlock()
    {
        var driver = new ControlledCaptureDriver();
        var processor = new ControlledCaptureProcessor();
        var dispatcher = new DesktopPreviewDispatcher();
        var clock = new ControlledCaptureClock();
        var coordinator = new CaptureCoordinator(
            driver.Registry,
            dispatcher,
            clock,
            NullLogger<CaptureCoordinator>.Instance
        );
        var view = new CaptureViewModel(coordinator, processor, dispatcher, clock)
        {
            AutomaticExposure = false,
            AutoCapture = true,
        };
        await view.StartAsync(CancellationToken.None);
        var pixels = ProcessingFrameFixtures.PublicFinger();
        var pending = await EmptySensorTestFrames.PrimeAsync(driver, clock);
        pending.Complete(pixels).Should().BeTrue();
        pending = await driver.NextAsync();
        processor.Entered.Task.IsCompleted.Should().BeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(999));
        pending.Complete(pixels).Should().BeTrue();
        pending = await driver.NextAsync();
        processor.Entered.Task.IsCompleted.Should().BeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        pending.Complete(pixels).Should().BeTrue();
        var frozen = await processor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        view.LiveFrame.Value.Frame.Sequence.Should().Be(6);
        processor.Release();
        await view.CaptureCommand.ExecutionTask!;
        view.Captured.HasValue.Should().BeTrue();
        view.CaptureGuidance.Should().Be("Captured");
        await view.StopAsync();
    }
}
