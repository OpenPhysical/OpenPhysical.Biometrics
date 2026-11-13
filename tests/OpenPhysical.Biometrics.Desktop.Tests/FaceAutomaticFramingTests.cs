// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Formats.Faces;

[TestFixture]
internal sealed class FaceAutomaticFramingTests
{
    [AvaloniaTest]
    public async Task DetectedPortraitCanBeAcceptedWithoutMarkingOrAdjustingAnyPoint()
    {
        await using var context = new PersonWorkspaceContext();
        context.Person.Name = "Example person";
        var policy = PortraitMouthPolicy.Create(.15).Value;
        (await context.Face.ConfigureMouthPolicyAsync(policy)).Should().Succeed();
        await context.Enrollment.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        await context.Face.Actions.Capture.ExecuteAsync(default);
        context.Face.ReviewConfirmed = true;
        context.Face.HasFailures.Should().BeFalse();
        context.Face.AcceptCommand.CanExecute(default).Should().BeTrue();
        await context.Face.AcceptCommand.ExecuteAsync(default);
        context.Face.Complete.Should().BeTrue();
        var accepted = context.Face.Accepted.Value;
        accepted.Portrait.Measurements.Value.ReviewedHeadBoundary.Should().BeFalse();
        accepted.Portrait.MouthPolicy.Should().Be(policy);
        accepted.PortraitReviewed.Should().BeTrue();
        Incits385Codec.Parse(accepted.ObjectBytes.AsSpan()).Should().Succeed();
    }

    [AvaloniaTest]
    public async Task AFailedPoseStillBlocksAcceptanceWhenNameAndPortraitReviewArePresent()
    {
        await using var context = new PersonWorkspaceContext(
            new FaceTestCameraProvider { FixtureRotation = 15 }
        );
        context.Person.Name = "Example person";
        await context.Face.ConfigureMouthPolicyAsync(PortraitMouthPolicy.Create(.15).Value);
        await context.Enrollment.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        await context.Face.Actions.Capture.ExecuteAsync(default);
        context.Face.ReviewConfirmed = true;
        context.Face.HasFailures.Should().BeTrue();
        context.Face.AcceptCommand.CanExecute(default).Should().BeFalse();
        await context.Face.AcceptCommand.ExecuteAsync(default);
        context.Face.Complete.Should().BeFalse();
        context.Face.Accepted.HasNoValue.Should().BeTrue();
    }
}
