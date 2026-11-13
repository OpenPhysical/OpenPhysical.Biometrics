// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Facial;

[TestFixture]
internal sealed class FacePoseWorkflowTests
{
    [AvaloniaTest]
    public async Task RejectedTiltAsksForARetakeInsteadOfOfferingCropCorrection()
    {
        var camera = new FaceTestCameraProvider { FixtureRotation = 15 };
        using var portraits = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);
        var face = new FaceCaptureViewModel(
            camera,
            portraits,
            new FacialRecordPreparer(NullLogger<FacialRecordPreparer>.Instance),
            new PersonEnrollmentViewModel { Name = "Example person" },
            NullLogger<FaceCaptureViewModel>.Instance
        );
        try
        {
            await face.ActivateAsync(true);
            await FaceCaptureTestActions.ManualPreviewAsync(face);
            await face.Actions.Capture.ExecuteAsync(default);
            AssertRejectedPreview(face);
            face.Instructions.Should().Contain("Retake").And.Contain("upright");
            face.RequiresRetake.Should().BeTrue();
            face.IsReviewing.Should().BeFalse();
            face.Progress.Should().Be("Retake needed");
            face.ReviewConfirmed = true;
            face.AcceptCommand.CanExecute(default).Should().BeFalse();
            face.Complete.Should().BeFalse();
            await face.Actions.Retake.ExecuteAsync(default);
            face.HasCapture.Should().BeFalse();
            face.IsLive.Should().BeTrue();
            face.RequiresRetake.Should().BeFalse();
            face.Image.ImageGuides.HasNoValue.Should().BeTrue();
        }
        finally
        {
            await face.StopAsync();
        }
    }

    private static bool AssertRejectedPreview(FaceCaptureViewModel face)
    {
        (face.Image.Scene.Value.Width, face.Image.Scene.Value.Height).Should().Be((480, 640));
        face.Image.ImageGuides.HasValue.Should().BeTrue();
        return true;
    }
}
