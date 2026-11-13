// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;

[TestFixture]
internal sealed class FaceCameraRecoveryTests
{
    [AvaloniaTest]
    public async Task TheFirstDelayedAcknowledgedFrameEnablesTheBoundCaptureButton()
    {
        var camera = new FaceTestCameraProvider { DeferFirstFrame = true };
        using var portraits = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);
        var face = Create(camera, portraits);
        var window = new Window
        {
            Content = new FaceCaptureView { DataContext = face },
            Width = 800,
            Height = 600,
        };
        window.Show();
        try
        {
            await face.ActivateAsync(true);
            var button = window
                .GetVisualDescendants()
                .OfType<Button>()
                .Single(control => Equals(control.Content, "Capture face"));
            face.Image.Scene.HasNoValue.Should().BeTrue();
            button.IsEffectivelyEnabled.Should().BeFalse();
            camera.PublishFirst().Should().Succeed();
            await FaceCaptureTestActions.ManualPreviewAsync(face);
            button.IsEffectivelyEnabled.Should().BeTrue();
            face.Actions.Capture.CanExecute(default).Should().BeTrue();
        }
        finally
        {
            await face.StopAsync();
            window.Close();
        }
    }

    [AvaloniaTest]
    public async Task CaptureRemainsDisabledUntilRefreshHasAnAcknowledgedFrame()
    {
        var camera = new FaceTestCameraProvider(deferOpen: true);
        using var portraits = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);
        var face = Create(camera, portraits);
        var refreshing = face.Actions.CameraRefresh.ExecuteAsync(default);
        await camera.Opening.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var captureAllowedWhileOpening = face.Actions.Capture.CanExecute(default);
        var openingWasBusy = face.IsBusy;
        await face.Actions.Capture.ExecuteAsync(default);
        camera.ReleaseOpen().Should().BeTrue();
        await refreshing;
        await FaceCaptureTestActions.ManualPreviewAsync(face);
        try
        {
            openingWasBusy.Should().BeTrue();
            captureAllowedWhileOpening.Should().BeFalse();
            face.HasCapture.Should().BeFalse();
            face.IsBusy.Should().BeFalse();
            face.Actions.Capture.CanExecute(default).Should().BeTrue();
            face.Instructions.Should().NotContain("Wait for a complete camera preview");
        }
        finally
        {
            await face.StopAsync();
        }
    }

    [AvaloniaTest]
    public async Task ARecoveredCameraShowsItsActualModeAndClearsThePreviousFailure()
    {
        var camera = Camera();
        using var portraits = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);
        var face = Create(new ReenumeratingTestCameraProvider(camera), portraits);
        var window = new Window
        {
            Content = new FaceCaptureView { DataContext = face },
            Width = 800,
            Height = 600,
        };
        window.Show();
        await face.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(face);
        camera
            .FailRead(new(CameraErrorCodes.InvalidFrame, "Invalid native frame"))
            .IsSuccess.Should()
            .BeTrue();
        await UntilAsync(() => !face.IsLive);
        face.Instructions.Should().Contain("did not provide a usable image");
        await face.Actions.CameraRefresh.ExecuteAsync(default);
        await FaceCaptureTestActions.ManualPreviewAsync(face);
        face.IsLive.Should().BeTrue();
        face.Actions.Capture.CanExecute(default).Should().BeTrue();
        face.Instructions.Should().NotContain("did not provide a usable image");
        ModeSelector(window).SelectedItem.Should().BeSameAs(face.SelectedMode);
        camera.OpenCount.Should().Be(2);
        await face.StopAsync();
        window.Close();
    }

    [AvaloniaTest]
    public async Task SelectingAnotherAdvertisedModeDrainsThePreviousSessionAndKeepsSelectionVisible()
    {
        var camera = Camera();
        using var portraits = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);
        var face = Create(camera, portraits);
        var window = new Window
        {
            Content = new FaceCaptureView { DataContext = face },
            Width = 800,
            Height = 600,
        };
        window.Show();
        await face.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(face);
        var mode = face.Modes[1];
        ModeSelector(window).SelectedItem = mode;
        await UntilAsync(() => camera.OpenCount == 2 && !face.IsBusy && face.Image.Scene.HasValue);
        face.SelectedMode.Should().BeSameAs(mode);
        camera.LastFrame.Value.Mode.Should().BeSameAs(mode);
        ModeSelector(window).SelectedItem.Should().BeSameAs(mode);
        camera.StopCount.Should().Be(1);
        face.IsLive.Should().BeTrue();
        await face.StopAsync();
        window.Close();
    }

    private static FaceTestCameraProvider Camera() =>
        new()
        {
            Source = new(
                "test-camera",
                "Public fixture camera",
                [new(808, 1024, 30, 1, "RGB24"), new(808, 1024, 15, 1, "RGB24")]
            ),
        };

    private static FaceCaptureViewModel Create(
        ICameraProvider camera,
        PortraitPreparer portraits
    ) =>
        new(
            camera,
            portraits,
            new FacialRecordPreparer(NullLogger<FacialRecordPreparer>.Instance),
            new PersonEnrollmentViewModel(),
            NullLogger<FaceCaptureViewModel>.Instance
        );

    private static ComboBox ModeSelector(Window window) =>
        window
            .GetVisualDescendants()
            .OfType<ComboBox>()
            .Single(control =>
                Avalonia.Automation.AutomationProperties.GetName(control) == "Native camera mode"
            );

    private static async Task UntilAsync(Func<bool> ready)
    {
        for (var i = 0; i < 200 && !ready(); i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
        ready().Should().BeTrue();
    }
}
