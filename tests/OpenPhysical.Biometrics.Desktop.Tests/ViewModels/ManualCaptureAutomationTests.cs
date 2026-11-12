// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ManualCaptureAutomationTests
{
    [AvaloniaTest]
    public async Task ContinuousManualHoldStaysAvailableAndEnablingAutoStartsANewDelay()
    {
        await using var rig = new ProcessingCaptureRig();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        var pending = await rig.StartAsync();
        for (var index = 0; index < 8; index++)
        {
            pending.Complete(pixels).Should().BeTrue();
            pending = await rig.Driver.NextAsync();
            rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
        }
        rig.View.Captured.HasNoValue.Should().BeTrue();
        rig.View.CaptureCommand.CanExecute(default).Should().BeTrue();
        rig.Services.Counts.Should().Be((0, 0, 0));
        rig.View.AutoCapture = true;
        pending.Complete(pixels).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        rig.View.Captured.HasNoValue.Should().BeTrue();
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        pending.Complete(pixels).Should().BeTrue();
        await CaptureWorkflowWait.UntilAsync(rig.View, () => rig.View.Captured.HasValue);
        rig.View.CaptureCommand.CanExecute(default).Should().BeFalse();
    }

    [AvaloniaTest]
    public async Task TwoEmptyManualFramesClearRequiredRemovalBeforeAutoIsEnabled()
    {
        await using var rig = new ProcessingCaptureRig();
        await rig.CaptureAsync();
        await rig.View.RetakeCommand.ExecuteAsync(default);
        var pixels = ProcessingFrameFixtures.PublicFinger();
        var empty = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        var pending = await rig.Driver.NextAsync();
        pending.Complete(pixels).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        rig.View.CaptureGuidance.Should()
            .Contain("Lift your finger")
            .And.Contain("clean the sensor");
        pending.Complete(empty).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        pending.Complete(empty).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        pending.Complete(pixels).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        rig.View.CaptureCommand.CanExecute(default).Should().BeTrue();
        rig.View.ConfigureAutoCaptureDelay(TimeSpan.Zero).Should().Succeed();
        rig.View.AutoCapture = true;
        pending.Complete(pixels).Should().BeTrue();
        await CaptureWorkflowWait.UntilAsync(rig.View, () => rig.View.Captured.HasValue);
    }
}
