// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class ManualPreviewEligibilityTests
{
    [AvaloniaTest]
    public async Task ManualCaptureWaitsForTheEditedPreviewAndRetainsItsExactImageAndRevision()
    {
        var driver = new ControlledCaptureDriver();
        using var processor = new ControlledCaptureProcessor();
        var clock = new ControlledCaptureClock();
        var view = NewView(driver, processor, clock);
        try
        {
            await view.StartAsync(CancellationToken.None);
            var visible = ProcessingFrameFixtures.PublicFinger();
            (await EmptySensorTestFrames.PrimeAsync(driver, clock))
                .Complete(visible)
                .Should()
                .BeTrue();
            var pending = await driver.NextAsync();
            var original = view.LiveFrame.Value.Frame;
            var edited = await EditPreviewAsync(view);
            var capturing = view.CaptureCommand.ExecuteAsync(default);
            pending.Complete().Should().BeTrue();
            var frozen = await processor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            view.LiveFrame.Value.Frame.Sequence.Should().Be(4);
            view.LiveFrame.Value.Frame.Pixels.Should().Equal(visible);
            frozen.Pixels.ToArray().Should().Equal(view.Scene.Value.GrayPixels);
            processor.Release();
            await capturing;
            view.Captured.Value.Frame.Should().BeSameAs(original);
            view.Captured.Value.Processing.Value.ProcessingRevision.Should()
                .Be(edited.ProcessingRevision);
            view.Captured.Value.Processing.Value.Artifact.Policy.Brightness.Should().Be(17);
        }
        finally
        {
            processor.Release();
            await view.StopAsync();
        }
    }

    private static async Task<FingerprintPreprocessingArtifact> EditPreviewAsync(
        CaptureViewModel view
    )
    {
        view.CaptureCommand.CanExecute(default).Should().BeTrue();
        view.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Create(false, 17, 0).Value,
                CapturePolicySettings.Default
            )
            .Should()
            .Succeed();
        view.CaptureCommand.CanExecute(default).Should().BeFalse();
        await CaptureWorkflowWait.UntilAsync(
            view,
            () => view.LiveFrame.Value.Processing.Value.Policy.Brightness == 17
        );
        view.CaptureCommand.CanExecute(default).Should().BeTrue();
        return view.LiveFrame.Value.Processing.Value;
    }

    private static CaptureViewModel NewView(
        ControlledCaptureDriver driver,
        ControlledCaptureProcessor processor,
        ControlledCaptureClock clock
    )
    {
        var dispatcher = new DesktopPreviewDispatcher();
        var coordinator = new CaptureCoordinator(
            driver.Registry,
            dispatcher,
            clock,
            NullLogger<CaptureCoordinator>.Instance
        );
        return new CaptureViewModel(coordinator, processor, dispatcher, clock)
        {
            AutoCapture = false,
            AutomaticExposure = false,
        };
    }
}
