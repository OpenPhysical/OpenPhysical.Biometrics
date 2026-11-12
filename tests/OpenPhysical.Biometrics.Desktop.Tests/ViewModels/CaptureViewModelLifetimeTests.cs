// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureViewModelLifetimeTests
{
    [AvaloniaTest]
    public async Task ResetDefaultsPreservesTheSelectedPlanFingerAndRearmsWithDarkerExposure()
    {
        var view = NewView(
            new ControlledCaptureDriver(),
            new ControlledCaptureProcessor(),
            new ControlledCaptureClock()
        );
        view.SelectedFinger = FingerChoice.For(
            OpenPhysical.Biometrics.Fingerprints.FingerPosition.LeftRing
        );
        view.Exposure = 10;
        (await view.ResetDefaultsAsync()).Should().Succeed();
        view.SelectedFinger.Position.Should()
            .Be(OpenPhysical.Biometrics.Fingerprints.FingerPosition.LeftRing);
        view.Exposure.Should().Be(220);
        view.AutomaticExposure.Should().BeTrue();
        await view.StopAsync();
    }

    [AvaloniaTest]
    public async Task ShutdownDrainsProcessingSuppressesLateCaptureAndReleasesTheScene()
    {
        var driver = new ControlledCaptureDriver();
        var processor = new ControlledCaptureProcessor();
        var clock = new ControlledCaptureClock();
        var view = NewView(driver, processor, clock);
        await view.StartAsync(CancellationToken.None);
        (await EmptySensorTestFrames.PrimeAsync(driver, clock)).Complete(
            ProcessingFrameFixtures.PublicFinger()
        );
        var pending = await driver.NextAsync();
        var capturing = view.CaptureCommand.ExecuteAsync(default);
        pending.Complete();
        await processor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var status = view.Status;
        var stopping = view.StopAsync();
        stopping.IsCompleted.Should().BeFalse();
        processor.Release();
        (await stopping).Should().Succeed();
        await capturing;
        view.Captured.HasNoValue.Should().BeTrue();
        view.Scene.HasNoValue.Should().BeTrue();
        view.Status.Should().Be(status);
        view.Report(new DesktopError("Late", "Late update"));
        view.Status.Should().Be(status);
        view.RetakeCommand.CanExecute(default).Should().BeFalse();
    }

    private static CaptureViewModel NewView(
        ControlledCaptureDriver driver,
        ICaptureProcessor processor,
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
            AutomaticExposure = false,
            AutoCapture = false,
        };
    }
}
