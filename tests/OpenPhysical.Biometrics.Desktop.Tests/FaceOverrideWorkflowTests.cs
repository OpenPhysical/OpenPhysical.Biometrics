// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Facial;

[TestFixture]
internal sealed class FaceOverrideWorkflowTests
{
    [AvaloniaTest]
    public async Task OverrideUnlocksReviewWithoutAcceptingAndPreservesTheFailedTests()
    {
        await using var context = new PersonWorkspaceContext(
            new FaceTestCameraProvider { FixtureRotation = 15 }
        );
        context.Person.Name = "Example person";
        await CaptureRejectedAsync(context);
        var message = context.Face.FailureMessage;
        message.Should().StartWith("Photo failed the following tests: ");
        await context.Face.Actions.Override.ExecuteAsync(default);
        context.Face.RequiresRetake.Should().BeFalse();
        context.Face.IsReviewing.Should().BeTrue();
        context.Face.Complete.Should().BeFalse();
        context.Enrollment.FaceActive.Should().BeTrue();
        context.Face.FailureMessage.Should().Be(message);
        context.Face.AcceptCommand.CanExecute(default).Should().BeFalse();
        await context.AcceptFaceAsync();
        var accepted = context.Face.Accepted.Value;
        var receipt = accepted.CaptureOverride.Value;
        receipt.SourceSha256.Should().Be(accepted.Portrait.SourceSha256);
        receipt
            .FindingCodes.Should()
            .BeEquivalentTo(
                accepted
                    .Portrait.Findings.Where(value => value.Action == PortraitFindingAction.Retake)
                    .Select(value => value.Code)
                    .Distinct()
            );
        accepted.Acquisition.FullFrontalConfirmed.Should().BeFalse();
        context.Face.FailureMessage.Should().Be(message);
    }

    [AvaloniaTest]
    public async Task RetakeRequiresANewOverrideEvenWhenTheFixturePixelsRepeat()
    {
        await using var context = new PersonWorkspaceContext(
            new FaceTestCameraProvider { FixtureRotation = 15 }
        );
        context.Person.Name = "Example person";
        await CaptureRejectedAsync(context);
        await context.Face.Actions.Override.ExecuteAsync(default);
        await context.Face.Actions.Retake.ExecuteAsync(default);
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        await context.Face.Actions.Capture.ExecuteAsync(default);
        context.Face.RequiresRetake.Should().BeTrue();
        context.Face.Actions.Override.CanExecute(default).Should().BeTrue();
        context.Face.AcceptCommand.CanExecute(default).Should().BeFalse();
    }

    private static async Task CaptureRejectedAsync(PersonWorkspaceContext context)
    {
        await context.Enrollment.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        await context.Face.Actions.Capture.ExecuteAsync(default);
        context.Face.RequiresRetake.Should().BeTrue();
        context.Face.HasFailures.Should().BeTrue();
    }
}
