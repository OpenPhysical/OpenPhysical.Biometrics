// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FaceGuideWorkflowTests
{
    [AvaloniaTest]
    public async Task PivGuidesAppearOnlyAfterCaptureWhileLiveDetectionRemainsAvailable()
    {
        await using var context = new PersonWorkspaceContext();
        var face = context.Face;
        face.Image.ShowFaceDetection = true;
        var view = new FaceCaptureView { DataContext = face };
        var window = new Window
        {
            Content = view,
            Width = 800,
            Height = 600,
        };
        window.Show();
        await face.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(face);
        await Task.Delay(350);
        context.Camera.PublishNext().Should().Succeed();
        await DetectedAsync(face);
        face.Image.ImageGuides.HasNoValue.Should().BeTrue();
        var checkboxes = view.GetVisualDescendants().OfType<CheckBox>().ToArray();
        checkboxes
            .Single(control => Equals(control.Content, "PIV guides"))
            .IsVisible.Should()
            .BeFalse();
        checkboxes
            .Single(control => Equals(control.Content, "Face detection"))
            .IsVisible.Should()
            .BeTrue();
        await face.Actions.Capture.ExecuteAsync(default);
        face.Image.ImageGuides.HasValue.Should().BeTrue();
        checkboxes
            .Single(control => Equals(control.Content, "PIV guides"))
            .IsVisible.Should()
            .BeTrue();
        await face.Actions.Retake.ExecuteAsync(default);
        await FaceCaptureTestActions.ManualPreviewAsync(face);
        face.Image.ImageGuides.HasNoValue.Should().BeTrue();
        window.Close();
    }

    [AvaloniaTest]
    public async Task GuidesAndDetectionToggleIndependentlyWithoutChangingTheAcceptedPortrait()
    {
        await using var context = new PersonWorkspaceContext();
        context.Person.Name = "Example person";
        await context.PrepareFaceAsync();
        await context.AcceptFaceAsync();
        var face = context.Face;
        var portrait = face.Image.Scene.Value;
        var accepted = face.Accepted.Value;
        face.Image.ShowPivGuides.Should().BeTrue();
        face.Image.ShowFaceDetection.Should().BeFalse();
        var guides = face.Image.ImageGuides.Value;
        face.Image.DetectionGuides.HasNoValue.Should().BeTrue();
        face.Image.ShowPivGuides = false;
        face.Image.ImageGuides.HasNoValue.Should().BeTrue();
        face.Image.ShowFaceDetection = true;
        var detection = face.Image.DetectionGuides.Value;
        face.Image.ShowPivGuides = true;
        face.Image.ImageGuides.Value.Should().BeSameAs(guides);
        face.Image.ShowFaceDetection = false;
        face.Image.DetectionGuides.HasNoValue.Should().BeTrue();
        face.Image.ShowFaceDetection = true;
        face.Image.DetectionGuides.Value.Should().BeSameAs(detection);
        face.Image.Scene.Value.Should().BeSameAs(portrait);
        face.Accepted.Value.Should().BeSameAs(accepted);
        face.IsBusy.Should().BeFalse();
    }

    private static async Task DetectedAsync(FaceCaptureViewModel face)
    {
        for (var i = 0; i < 200 && face.Image.DetectionGuides.HasNoValue; i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
        face.Image.DetectionGuides.HasValue.Should().BeTrue();
    }
}
