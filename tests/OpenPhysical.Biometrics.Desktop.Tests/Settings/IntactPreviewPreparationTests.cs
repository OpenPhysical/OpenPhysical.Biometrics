// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class IntactPreviewPreparationTests
{
    [AvaloniaTest]
    public async Task DefaultContrastDarkensPreviewAndOriginalRestoresItsExactSource()
    {
        await using var rig = new ProcessingCaptureRig();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        (await rig.StartAsync()).Complete(pixels).Should().BeTrue();
        await rig.Driver.NextAsync();
        var shown = rig.View.LiveFrame.Value.Processing.Value;
        shown.Pixels.Zip(pixels).Should().OnlyContain(pair => pair.First <= pair.Second);
        shown.Pixels.Zip(pixels).Count(pair => pair.First < pair.Second).Should().BeGreaterThan(0);
        rig.View.Scene.Value.GrayPixels.Should().Equal(shown.Pixels);
        rig.View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Original,
                CapturePolicySettings.Default
            )
            .Should()
            .Succeed();
        await CaptureWorkflowWait.UntilAsync(
            rig.View,
            () => rig.View.LiveFrame.Value.Processing.Value.Policy.IsOriginal
        );
        rig.View.Scene.Value.GrayPixels.Should().Equal(pixels);
        shown.OriginalFrame.Pixels.Should().Equal(pixels);
        rig.Services.Counts.Should().Be((0, 0, 0));
    }

    [AvaloniaTest]
    public async Task PreviewDefersSegmentationAndLocalEnhancementButAppliesWholeImageTone()
    {
        await using var rig = new ProcessingCaptureRig();
        var choices = new CapturePolicyViewModel { EnhancementPercent = 50 };
        rig.View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Create(true, 0, 0).Value,
                choices.Value
            )
            .Should()
            .Succeed();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        (await rig.StartAsync()).Complete(pixels).Should().BeTrue();
        await rig.Driver.NextAsync();
        var original = rig.View.LiveFrame.Value.Frame;
        AssertIntact(rig.View, pixels);
        rig.View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Create(true, 0, 20).Value,
                choices.Value
            )
            .Should()
            .Succeed();
        await CaptureWorkflowWait.UntilAsync(
            rig.View,
            () => rig.View.LiveFrame.Value.Processing.Value.Policy.Contrast == 20
        );
        var toned = rig.View.LiveFrame.Value.Processing.Value;
        toned.Policy.ClaheEnabled.Should().BeFalse();
        toned.Mask.Retained.Should().OnlyContain(value => value == 1);
        toned.OriginalFrame.Should().BeSameAs(original);
        toned
            .Pixels.Should()
            .Equal(
                pixels.Select(value =>
                    (byte)Math.Clamp(Math.Round(255 - (255 - value) * 1.3), 0, 255)
                )
            );
        original.Pixels.Should().Equal(pixels);
        rig.Services.Counts.Should().Be((0, 0, 0));
    }

    private static bool AssertIntact(CaptureViewModel view, byte[] pixels)
    {
        var preview = view.LiveFrame.Value.Processing.Value;
        preview.Pixels.Should().Equal(pixels);
        preview.Mask.Retained.Should().OnlyContain(value => value == 1);
        preview.Policy.ClaheEnabled.Should().BeFalse();
        view.Scene.Value.Foreground.Should().BeEmpty();
        return true;
    }
}
