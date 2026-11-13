// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class CaptureAutomationTests
{
    [AvaloniaTest]
    public async Task RejectedContactCannotSatisfyTheRemovalRequiredForAnotherCapture()
    {
        await using var rig = new ProcessingCaptureRig();
        await rig.CaptureAsync();
        rig.View.ConfigureAutoCaptureDelay(TimeSpan.Zero).Should().Succeed();
        rig.View.AutoCapture = true;
        await rig.View.RetakeCommand.ExecuteAsync(default);
        var pending = await rig.Driver.NextAsync();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        var pale = pixels.Select(value => (byte)(235 + value * 20 / 255)).ToArray();
        for (var index = 0; index < 3; index++)
        {
            pending.Complete(pale).Should().BeTrue();
            pending = await rig.Driver.NextAsync();
        }
        pending.Complete(pixels).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        rig.View.CaptureGuidance.Should().Be("Lift your finger, then place the next finger.");
        rig.View.CaptureCommand.CanExecute(default).Should().BeFalse();
        rig.View.Captured.HasNoValue.Should().BeTrue();
        var empty = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        pending.Complete(empty).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        pending.Complete(empty).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        pending.Complete(pixels).Should().BeTrue();
        await CaptureWorkflowWait.UntilAsync(rig.View, () => rig.View.Captured.HasValue);
    }
}
