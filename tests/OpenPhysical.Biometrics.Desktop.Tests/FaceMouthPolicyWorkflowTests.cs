// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Facial;

[TestFixture]
internal sealed class FaceMouthPolicyWorkflowTests
{
    [AvaloniaTest]
    public async Task PolicyChangeRechecksTheDraftWithoutReplacingPixelsOrKeepingAnOldOverride()
    {
        await using var context = new PersonWorkspaceContext(
            new FaceTestCameraProvider { FixtureRotation = 15 }
        );
        context.Person.Name = "Example person";
        await context.Enrollment.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        await context.Face.Actions.Capture.ExecuteAsync(default);
        await context.Face.Actions.Override.ExecuteAsync(default);
        context.Face.ReviewConfirmed = true;
        var image = context.Face.Image.Scene.Value;
        var before = context.Face.FailureMessage;
        var relaxed = PortraitMouthPolicy.Create(.15).Value;
        await context.Face.ConfigureMouthPolicyAsync(relaxed);
        context.Face.Image.Scene.Value.Should().BeSameAs(image);
        context.Face.ReviewConfirmed.Should().BeFalse();
        context.Face.RequiresRetake.Should().BeTrue();
        context.Face.Actions.Override.CanExecute(default).Should().BeTrue();
        context.Face.FailureMessage.Should().NotBe(before);
        context.Face.Complete.Should().BeFalse();
    }

    [AvaloniaTest]
    public async Task PolicyChangeKeepsAnAcceptedObjectAndAppliesToTheNextCapture()
    {
        await using var context = new PersonWorkspaceContext();
        context.Person.Name = "Example person";
        await context.PrepareFaceAsync();
        await context.AcceptFaceAsync();
        var accepted = context.Face.Accepted.Value;
        var relaxed = PortraitMouthPolicy.Create(.15).Value;
        await context.Face.ConfigureMouthPolicyAsync(relaxed);
        context.Face.Accepted.Value.Should().BeSameAs(accepted);
        await context.SelectAsync("face");
        await context.Face.Actions.Retake.ExecuteAsync(default);
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        await context.Face.Actions.Capture.ExecuteAsync(default);
        context.Face.RequiresRetake.Should().BeFalse();
        context.Face.HasFailures.Should().BeFalse();
        await FaceCaptureTestActions.OverrideRejectedAsync(context.Face);
        await context.AcceptFaceAsync();
        context.Face.Accepted.Value.Portrait.MouthPolicy.Should().Be(relaxed);
    }

    [AvaloniaTest]
    public async Task UnusablePhotoDoesNotBlockPolicyChangesOrAllowAnOverride()
    {
        await using var context = new PersonWorkspaceContext(
            new FaceTestCameraProvider { EmptyImage = true }
        );
        context.Person.Name = "Example person";
        await context.Enrollment.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        await context.Face.Actions.Capture.ExecuteAsync(default);
        var policy = PortraitMouthPolicy.Create(.15).Value;
        (await context.Face.ConfigureMouthPolicyAsync(policy)).Should().Succeed();
        context.Face.HasFailures.Should().BeTrue();
        context.Face.CanOverride.Should().BeFalse();
        context.Face.AcceptCommand.CanExecute(default).Should().BeFalse();
        await context.Face.ResetAsync();
        (await context.Face.ConfigureMouthPolicyAsync(policy)).Should().Succeed();
        context.Face.Complete.Should().BeFalse();
    }
}
