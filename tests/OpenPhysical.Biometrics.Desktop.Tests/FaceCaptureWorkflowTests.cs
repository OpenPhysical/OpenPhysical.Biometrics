// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Imaging;

[TestFixture]
internal sealed class FaceCaptureWorkflowTests
{
    [AvaloniaTest]
    public async Task CameraCaptureRequiresPortraitReviewAndAcceptanceThenKeepsRawOnlyInMemory()
    {
        var camera = new FaceTestCameraProvider();
        using var portraits = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);
        var person = new PersonEnrollmentViewModel { Name = "Example person" };
        var face = new FaceCaptureViewModel(
            camera,
            portraits,
            new FacialRecordPreparer(NullLogger<FacialRecordPreparer>.Instance),
            person,
            NullLogger<FaceCaptureViewModel>.Instance
        );
        (await face.ActivateAsync(true)).Should().Succeed();
        await FaceCaptureTestActions.ManualPreviewAsync(face);
        face.OpticalQualificationApplied(camera.Source.Id, face.SelectedMode, true)
            .Should()
            .Succeed();
        await face.Actions.Capture.ExecuteAsync(default);
        face.HasCapture.Should().BeTrue();
        face.AcceptCommand.CanExecute(default).Should().BeFalse();
        await FaceCaptureTestActions.OverrideRejectedAsync(face);
        face.Complete.Should().BeFalse();
        face.ReviewConfirmed = true;
        face.AcceptCommand.CanExecute(default).Should().BeTrue();
        await face.AcceptCommand.ExecuteAsync(default);
        face.Complete.Should().BeTrue();
        AssertAcceptedObject(face, camera);
        await face.StopAsync();
        camera.LastFrame.Value.Image.CopyRgbBytes().Should().Fail();
        face.Accepted.HasNoValue.Should().BeTrue();
        camera.OpenCount.Should().Be(1);
        camera.StopCount.Should().Be(1);
    }

    [AvaloniaTest]
    public async Task PendingReviewKeepsPixelsAndModeEvidenceIsNotTransferable()
    {
        var camera = new FaceTestCameraProvider();
        using var portraits = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);
        var face = new FaceCaptureViewModel(
            camera,
            portraits,
            new FacialRecordPreparer(NullLogger<FacialRecordPreparer>.Instance),
            new PersonEnrollmentViewModel(),
            NullLogger<FaceCaptureViewModel>.Instance
        );
        await face.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(face);
        face.OpticalQualificationApplied("wrong-device", face.SelectedMode, true).Should().Fail();
        face.QualificationRequired.Should().BeTrue();
        await face.Actions.Capture.ExecuteAsync(default);
        var original = face.Image.Scene.Value;
        face.ReviewConfirmed = true;
        face.Complete.Should().BeFalse();
        face.Image.Scene.Value.Should().BeSameAs(original);
        face.AcceptCommand.CanExecute(default).Should().BeFalse();
        await face.ActivateAsync(false);
        camera.OpenCount.Should().Be(1);
        await face.StopAsync();
    }

    [AvaloniaTest]
    public async Task DisconnectCannotCaptureThePreviousAcknowledgedPreview()
    {
        var camera = new FaceTestCameraProvider();
        using var portraits = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);
        var face = new FaceCaptureViewModel(
            camera,
            portraits,
            new FacialRecordPreparer(NullLogger<FacialRecordPreparer>.Instance),
            new PersonEnrollmentViewModel(),
            NullLogger<FaceCaptureViewModel>.Instance
        );
        await face.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(face);
        camera.Unplug().Should().Succeed();
        for (var i = 0; i < 100 && face.IsLive; i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
        face.IsLive.Should().BeFalse();
        face.Image.Scene.HasNoValue.Should().BeTrue();
        face.Actions.Capture.CanExecute(default).Should().BeFalse();
        face.Complete.Should().BeFalse();
        camera.LastFrame.Value.Image.CopyRgbBytes().Should().Fail();
        await face.StopAsync();
    }

    [AvaloniaTest]
    public async Task ShutdownDrainsAnOpeningCameraAndDisposesTheLateFrame()
    {
        var camera = new FaceTestCameraProvider(deferOpen: true);
        using var portraits = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);
        var face = new FaceCaptureViewModel(
            camera,
            portraits,
            new FacialRecordPreparer(NullLogger<FacialRecordPreparer>.Instance),
            new PersonEnrollmentViewModel(),
            NullLogger<FaceCaptureViewModel>.Instance
        );
        var activating = face.ActivateAsync(true);
        await camera.Opening.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var captureAllowedWhileOpening = face.Actions.Capture.CanExecute(default);
        var stopping = face.StopAsync();
        var completedBeforeOpen = stopping.IsCompleted;
        camera.ReleaseOpen().Should().BeTrue();
        await Task.WhenAll(activating, stopping);
        captureAllowedWhileOpening.Should().BeFalse();
        completedBeforeOpen.Should().BeFalse();
        face.IsLive.Should().BeFalse();
        face.Image.Scene.HasNoValue.Should().BeTrue();
        face.Actions.Capture.CanExecute(default).Should().BeFalse();
        camera.LastFrame.Value.Image.CopyRgbBytes().Should().Fail();
        camera.OpenCount.Should().Be(1);
        camera.StopCount.Should().Be(1);
    }

    private static bool AssertAcceptedObject(
        FaceCaptureViewModel face,
        FaceTestCameraProvider camera
    )
    {
        Incits385Codec.Parse(face.Accepted.Value.ObjectBytes.AsSpan()).Should().Succeed();
        camera.LastFrame.Value.Image.CopyRgbBytes().Should().Succeed();
        return true;
    }
}
