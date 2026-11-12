// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture.Futronic;

[TestFixture]
internal sealed class LiveProcessingDetailsTests
{
    [AvaloniaTest]
    public async Task LightObservationRetainsExactVisiblePreviewAndRawDetailsWithoutNativeWork()
    {
        await using var rig = new ProcessingCaptureRig();
        var pixels = ProcessingFrameFixtures
            .PublicFinger()
            .Select(value => (byte)(235 + value * 20 / 255))
            .ToArray();
        var request = await rig.StartAsync();
        request.Complete(pixels).Should().BeTrue();
        var pending = await rig.Driver.NextAsync();
        var frame = (await request.Task).Value;
        var contact = Fs50ContactAnalysis.Analyze(pixels).Value;
        var details = rig.View.ProcessingDetails;
        rig.View.LiveFrame.Value.Frame.Should().BeSameAs(frame);
        rig.View.Scene.Value.GrayPixels.Should()
            .Equal(rig.View.LiveFrame.Value.Processing.Value.Pixels);
        rig.View.CaptureCommand.CanExecute(default).Should().BeFalse();
        details.Available.Should().BeTrue();
        details.Scene.HasNoValue.Should().BeTrue();
        details.Selected = "Original";
        details.SetOpen(true).Should().Succeed();
        details.Scene.Value.GrayPixels.Should().Equal(frame.Pixels);
        details.Description.Should().Contain("TooLight").And.Contain("Candidate:");
        details.Selected = "Candidate mask";
        details
            .Scene.Value.GrayPixels.Should()
            .Equal(
                contact.Diagnostics.CandidateMask.Select(value => value == 0 ? (byte)255 : (byte)0)
            );
        details.SetOpen(false).Should().Succeed();
        details.Scene.HasNoValue.Should().BeTrue();
        rig.Services.Counts.Should().Be((0, 0, 0));
        await ResetAsync(rig, pending);
        details.Available.Should().BeFalse();
        details.Scene.HasNoValue.Should().BeTrue();
    }

    [AvaloniaTest]
    public async Task StageSelectionSurvivesReplacementAndResetCannotRevealAnOldImage()
    {
        await using var rig = new ProcessingCaptureRig();
        (await rig.StartAsync()).Complete(ProcessingFrameFixtures.PublicFinger()).Should().BeTrue();
        var pending = await rig.Driver.NextAsync();
        var details = rig.View.ProcessingDetails;
        details.Selected = "Candidate mask";
        details.SetOpen(true).Should().Succeed();
        var previous = details.Scene.Value;
        var empty = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        pending.Complete(empty).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        details.Selected.Should().Be("Candidate mask");
        details.Scene.Value.Should().NotBeSameAs(previous);
        details.Scene.Value.GrayPixels.Should().OnlyContain(value => value == 255);
        await ResetAsync(rig, pending);
        details.Available.Should().BeFalse();
        details.Scene.HasNoValue.Should().BeTrue();
        details.Description.Should().BeEmpty();
        details.SetOpen(false).Should().Succeed();
        details.SetOpen(true).Should().Succeed();
        details.Scene.HasNoValue.Should().BeTrue();
    }

    private static async Task ResetAsync(ProcessingCaptureRig rig, ControlledCaptureRequest pending)
    {
        var reset = rig.View.ResetCaptureCommand.ExecuteAsync(default);
        pending.Complete(Enumerable.Repeat((byte)255, 800 * 750).ToArray()).Should().BeTrue();
        await reset;
    }
}
