// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed class FrozenSelectionTestContext : IAsyncDisposable
{
    private readonly ControlledCaptureDriver driver = new();
    private readonly ControlledCaptureProcessor processor = new();
    private readonly ControlledCaptureClock clock = new();
    private readonly CaptureCoordinator coordinator;
    internal CaptureViewModel View { get; }

    internal FrozenSelectionTestContext()
    {
        var dispatcher = new DesktopPreviewDispatcher();
        coordinator = new CaptureCoordinator(
            driver.Registry,
            dispatcher,
            clock,
            NullLogger<CaptureCoordinator>.Instance
        );
        View = new CaptureViewModel(coordinator, processor, dispatcher, clock)
        {
            AutomaticExposure = false,
        };
        View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Create(false, 7, 0).Value,
                CapturePolicySettings.Default
            )
            .Should()
            .Succeed();
    }

    internal async Task<CaptureFrame> SelectEarlierAsync()
    {
        await View.StartAsync(CancellationToken.None);
        var pixels = ProcessingFrameFixtures.PublicFinger();
        var next = await EmptySensorTestFrames.PrimeAsync(driver, clock);
        next.Complete(pixels).Should().BeTrue();
        next = await driver.NextAsync();
        clock.Advance(TimeSpan.FromMilliseconds(600));
        next.Complete(pixels).Should().BeTrue();
        next = await driver.NextAsync();
        clock.Advance(TimeSpan.FromMilliseconds(400));
        next.Complete(Noisier(pixels)).Should().BeTrue();
        var frozen = await processor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        View.LiveFrame.Value.Frame.Sequence.Should()
            .Be(5, "the earlier stable frame outranks the latest noisy frame");
        return View.LiveFrame.Value.Frame;
    }

    internal async Task<bool> AwaitRevisionAsync()
    {
        await CaptureWorkflowWait.UntilAsync(
            View,
            () => View.LiveFrame.Value.Processing.Value.Policy.Brightness == 9
        );
        return true;
    }

    private static byte[] Noisier(byte[] original)
    {
        var pixels = (byte[])original.Clone();
        var mask = Fs50ContactAnalysis.Analyze(original).Value.Mask;
        for (var index = 0; index < pixels.Length; index += 17)
            if (mask[index] != 0)
                pixels[index] = (byte)
                    Math.Clamp(pixels[index] + (index % 2 == 0 ? 20 : -20), 0, 255);
        return pixels;
    }

    public async ValueTask DisposeAsync()
    {
        processor.Release();
        await View.StopAsync();
        processor.Dispose();
    }
}
