// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class AdjustedPreviewReadinessTests
{
    [AvaloniaTest]
    public async Task ToneControlsCannotAuthorizeARawLightStartupContact()
    {
        await using var rig = new ProcessingCaptureRig();
        await rig.View.StartAsync(CancellationToken.None);
        var pixels = ProcessingFrameFixtures
            .PublicFinger()
            .Select(value => (byte)(235 + value * 20 / 255))
            .ToArray();
        (await rig.Driver.NextAsync()).Complete(pixels).Should().BeTrue();
        var pending = await rig.Driver.NextAsync();
        rig.View.CaptureCommand.CanExecute(default).Should().BeFalse();
        rig.View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Create(true, -70, 100).Value,
                CapturePolicySettings.Default
            )
            .Should()
            .Succeed();
        pending.Complete(pixels).Should().BeTrue();
        await rig.Driver.NextAsync();
        rig.View.LiveFrame.Value.Ready.Should().BeFalse();
        rig.View.Scene.HasValue.Should().BeTrue();
        rig.View.CaptureCommand.CanExecute(default).Should().BeFalse();
        rig.Services.Counts.Should().Be((0, 0, 0));
    }

    [AvaloniaTest]
    public async Task ChangingContactPolicyInvalidatesPreviousReadinessImmediately()
    {
        await using var rig = new ProcessingCaptureRig();
        (await rig.StartAsync()).Complete(ProcessingFrameFixtures.PublicFinger()).Should().BeTrue();
        await rig.Driver.NextAsync();
        rig.View.CaptureCommand.CanExecute(default).Should().BeTrue();
        var current = CapturePolicySettings.Default;
        var changed = CapturePolicySettings
            .Create(
                current.Enhancement,
                current.EvidenceOptions with
                {
                    InnerGuard = 9,
                },
                current.EmptySensorOptions,
                1,
                5,
                current.ContextMargin
            )
            .Value;
        var adjusted = NextBrightness(
            rig.View,
            0,
            rig.View.LiveFrame.Value.Processing.Value.ProcessingRevision + 1
        );
        rig.View.ConfigureImageAdjustments(ImageAdjustmentSettings.Default, changed)
            .Should()
            .Succeed();
        (await adjusted.WaitAsync(TimeSpan.FromSeconds(5))).Ready.Should().BeFalse();
        rig.View.CaptureCommand.CanExecute(default).Should().BeFalse();
    }

    private static Task<DisplayedFingerprintFrame> NextBrightness(
        CaptureViewModel view,
        int brightness,
        long minimumRevision = 0
    )
    {
        var changed = new TaskCompletionSource<DisplayedFingerprintFrame>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        view.PropertyChanged += (_, args) =>
        {
            if (
                args.PropertyName == nameof(view.LiveFrame)
                && view.LiveFrame.HasValue
                && view.LiveFrame.Value.Processing.Match(
                    image =>
                        image.Policy.Brightness == brightness
                        && image.ProcessingRevision >= minimumRevision,
                    () => false
                )
            )
                changed.TrySetResult(view.LiveFrame.Value);
        };
        return changed.Task;
    }
}
