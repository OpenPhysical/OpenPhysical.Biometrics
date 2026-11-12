// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class ContactRejectionTests
{
    [AvaloniaTest]
    public async Task RepeatedSpeckledMarksNeverFreezeOrStartFingerprintProcessing()
    {
        var driver = new ControlledCaptureDriver();
        var processor = new ControlledCaptureProcessor();
        var clock = new ControlledCaptureClock();
        var view = NewView(driver, processor, clock);
        try
        {
            await view.StartAsync(CancellationToken.None);
            var pending = await driver.NextAsync();
            for (var index = 0; index < 5; index++)
            {
                pending.Complete(SpeckledMark()).Should().BeTrue();
                pending = await NextWithoutProcessingAsync(driver, processor);
                view.CaptureCommand.CanExecute(default).Should().BeFalse();
                clock.Advance(TimeSpan.FromMilliseconds(500));
            }
            view.Captured.HasNoValue.Should().BeTrue();
            view.CaptureGuidance.Should().NotContain("Capturing");
        }
        finally
        {
            processor.Release();
            await view.StopAsync();
        }
    }

    [AvaloniaTest]
    public async Task LightContactSuppressesTheDeadlineAndValidRecoveryClearsItsWarning()
    {
        var driver = new ControlledCaptureDriver();
        var processor = new ControlledCaptureProcessor();
        var clock = new ControlledCaptureClock();
        var view = NewView(driver, processor, clock);
        var finger = ProcessingFrameFixtures.PublicFinger();
        var light = finger.Select(value => (byte)(235 + value * 20 / 255)).ToArray();
        var frames = new[] { finger, finger, light, light, finger };
        try
        {
            await view.StartAsync(CancellationToken.None);
            var pending = await EmptySensorTestFrames.PrimeAsync(driver, clock);
            for (var index = 0; index < frames.Length; index++)
            {
                pending.Complete(frames[index]).Should().BeTrue();
                pending = await NextWithoutProcessingAsync(driver, processor);
                if (index is 2 or 3)
                {
                    view.CaptureGuidance.Should()
                        .Contain("too light")
                        .And.Contain("Clean the sensor")
                        .And.Contain("dry skin");
                    view.CaptureCommand.CanExecute(default).Should().BeFalse();
                }
                clock.Advance(TimeSpan.FromMilliseconds(500));
            }
            view.CaptureGuidance.Should()
                .Contain("Keep your finger here")
                .And.NotContain("too light");
            view.HasProblem.Should().BeFalse();
            view.CaptureCommand.CanExecute(default).Should().BeTrue();
        }
        finally
        {
            processor.Release();
            await view.StopAsync();
        }
    }

    private static CaptureViewModel NewView(
        ControlledCaptureDriver driver,
        ControlledCaptureProcessor processor,
        ControlledCaptureClock clock
    )
    {
        var dispatcher = new DesktopPreviewDispatcher();
        return new CaptureViewModel(
            new CaptureCoordinator(
                driver.Registry,
                dispatcher,
                clock,
                NullLogger<CaptureCoordinator>.Instance
            ),
            processor,
            dispatcher,
            clock
        )
        {
            AutomaticExposure = false,
        };
    }

    private static async Task<ControlledCaptureRequest> NextWithoutProcessingAsync(
        ControlledCaptureDriver driver,
        ControlledCaptureProcessor processor
    )
    {
        var next = driver.NextAsync();
        await Task.WhenAny(next, processor.Entered.Task);
        processor
            .Entered.Task.IsCompleted.Should()
            .BeFalse("rejected contact must not freeze a candidate");
        return await next;
    }

    private static byte[] SpeckledMark()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        uint state = 42;
        for (var y = 319; y <= 431; y++)
        for (var x = 366; x <= 434; x++)
        {
            if (Math.Pow((x - 400) / 34.0, 2) + Math.Pow((y - 375) / 56.0, 2) > 1)
                continue;
            state = unchecked(state * 1664525 + 1013904223);
            pixels[y * 800 + x] = (byte)(90 + state % 151);
        }
        return pixels;
    }
}
