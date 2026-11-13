// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FaceAutoCaptureTests
{
    [AvaloniaTest]
    public async Task SettledFaceFreezesTheAcknowledgedFrameWithoutAcceptingIt()
    {
        await using var context = new FaceAutoCaptureContext();
        context.Face.AutoCapture.Should().BeFalse();
        await context.StartAsync(TimeSpan.FromMilliseconds(300));
        await context.ObserveAsync();
        context.Face.HasCapture.Should().BeFalse();
        await context.ObserveAsync();
        var capturedAt = context.Camera.LastFrame.Value.CapturedAt;
        await FaceAutoCaptureContext.UntilAsync(() =>
            context.Face.HasCapture && !context.Face.IsBusy
        );
        context.Face.HasCapture.Should().BeTrue();
        context.Face.IsLive.Should().BeFalse();
        context.Face.Complete.Should().BeFalse();
        context.Face.ReviewConfirmed.Should().BeFalse();
        context.Camera.StopCount.Should().Be(1);
        await FaceCaptureTestActions.OverrideRejectedAsync(context.Face);
        context.Face.ReviewConfirmed = true;
        await context.Face.AcceptCommand.ExecuteAsync(default);
        context.Face.Accepted.Value.Acquisition.CapturedAt.Should().Be(capturedAt);
        context.Camera.OpenCount.Should().Be(1);
    }

    [AvaloniaTest]
    public async Task OpenMouthBlocksAutomaticCaptureUntilTheConfiguredPolicyAllowsIt()
    {
        await using var context = new FaceAutoCaptureContext();
        await context.StartAsync(TimeSpan.Zero, false);
        await context.ObserveAsync();
        context.Face.HasCapture.Should().BeFalse();
        context.Face.Instructions.Should().Be("Close your lips gently.");
        (
            await context.Face.ConfigureMouthPolicyAsync(
                OpenPhysical.Biometrics.Facial.PortraitMouthPolicy.Create(.15).Value
            )
        )
            .Should()
            .Succeed();
        await context.ObserveAsync();
        await FaceAutoCaptureContext.UntilAsync(() =>
            context.Face.HasCapture && !context.Face.IsBusy
        );
        context.Face.HasCapture.Should().BeTrue();
        context.Face.Complete.Should().BeFalse();
    }

    [AvaloniaTest]
    public async Task NoFaceCannotStartAnAutomaticCaptureEvenWithNoDelay()
    {
        await using var context = new FaceAutoCaptureContext(true);
        await context.StartAsync(TimeSpan.Zero);
        await context.ObserveAsync();
        context.Face.Instructions.Should().Be("Position your face in view.");
        context.Face.HasCapture.Should().BeFalse();
        context.Camera.StopCount.Should().Be(0);
    }

    [AvaloniaTest]
    public async Task ReapplyingUnchangedSettingsPreservesTheCurrentSettlingInterval()
    {
        await using var context = new FaceAutoCaptureContext();
        var delay = TimeSpan.FromMilliseconds(300);
        await context.StartAsync(delay);
        await context.ObserveAsync();
        context.Face.HasCapture.Should().BeFalse();
        (
            await context.Face.ConfigureMouthPolicyAsync(
                OpenPhysical.Biometrics.Facial.PortraitMouthPolicy.Create(.15).Value
            )
        )
            .Should()
            .Succeed();
        (await context.Face.ConfigureAutoCaptureAsync(true, delay)).Should().Succeed();
        await context.ObserveAsync();
        context.Face.HasCapture.Should().BeTrue();
        await FaceAutoCaptureContext.UntilAsync(() =>
            context.Face.HasCapture && !context.Face.IsBusy
        );
    }

    [AvaloniaTest]
    public async Task DisablingAndResettingDiscardThePreviousSettlingInterval()
    {
        await using var context = new FaceAutoCaptureContext();
        await context.StartAsync(TimeSpan.FromMilliseconds(300));
        await context.ObserveAsync();
        (await context.Face.ConfigureAutoCaptureAsync(false, TimeSpan.Zero)).Should().Succeed();
        await context.ObserveAsync();
        context.Face.HasCapture.Should().BeFalse();
        context.Face.AutoCapture.Should().BeFalse();
        (await context.Face.ResetAsync()).Should().Succeed();
        (await context.Face.ConfigureAutoCaptureAsync(true, TimeSpan.FromMilliseconds(300)))
            .Should()
            .Succeed();
        await context.ObserveAsync();
        context.Face.HasCapture.Should().BeFalse();
        await context.ObserveAsync();
        await FaceAutoCaptureContext.UntilAsync(() =>
            context.Face.HasCapture && !context.Face.IsBusy
        );
        context.Face.HasCapture.Should().BeTrue();
        context.Camera.OpenCount.Should().Be(2);
    }
}
