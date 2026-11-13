// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Facial;

[TestFixture]
internal sealed class FaceBackgroundWorkflowTests
{
    [AvaloniaTest]
    public async Task LaterMouthSettingsCannotDiscardAPendingBackgroundChange()
    {
        await using var context = new FaceAutoCaptureContext();
        await context.StartAsync(TimeSpan.FromSeconds(10));
        await context.Face.Actions.Capture.ExecuteAsync(default);
        context.Face.Details.Should().Contain("Background replaced");
        var originalScene = context.Face.Image.Scene.Value;
        var background = context.Face.ConfigureBackgroundAsync(false);
        var mouth = context.Face.ConfigureMouthPolicyAsync(PortraitMouthPolicy.Create(.16).Value);
        foreach (var outcome in await Task.WhenAll(background, mouth))
            outcome.Should().Succeed();
        context.Face.Details.Should().NotContain("Background replaced");
        context.Face.Image.Scene.Value.Should().NotBeSameAs(originalScene);
        context.Face.ReviewConfirmed.Should().BeFalse();
        context.Face.ReviewConfirmed = true;
        context.Face.AcceptCommand.CanExecute(default).Should().BeTrue();
        await context.Face.AcceptCommand.ExecuteAsync(default);
        context.Face.Accepted.Value.Portrait.Background.HasNoValue.Should().BeTrue();
    }
}
