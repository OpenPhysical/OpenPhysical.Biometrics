// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class OriginalCapturePipelineTests
{
    [AvaloniaTest]
    public async Task OriginalPreviewAndFrozenCapturePreservePixelsAndSeparateContextFromFeatures()
    {
        await using var rig = new ProcessingCaptureRig();
        rig.View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Original,
                CapturePolicySettings.Default
            )
            .IsSuccess.Should()
            .BeTrue();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        (await rig.StartAsync()).Complete(pixels).Should().BeTrue();
        var pending = await rig.Driver.NextAsync();
        var frame = rig.View.LiveFrame.Value.Frame;
        rig.View.Scene.Value.GrayPixels.Should().Equal(pixels);
        rig.View.LiveFrame.Value.Processing.Value.Policy.IsOriginal.Should().BeTrue();
        rig.Services.Counts.Should().Be((0, 0, 0));
        var capturing = rig.View.CaptureCommand.ExecuteAsync(default);
        pending.Complete().Should().BeTrue();
        await capturing;
        var capture = rig.View.Captured.Value;
        capture.Frame.Should().BeSameAs(frame);
        capture.Processing.Value.Artifact.Pixels.Should().Equal(pixels);
        rig.View.Scene.Value.GrayPixels.Should().Equal(pixels);
        var evidence = capture.Processing.Value;
        evidence.Artifact.Mask.Retained.Should().Equal(evidence.Contour.ContextMask);
        evidence.Artifact.Mask.Inner.Should().Equal(evidence.Contour.FeatureMask);
        evidence.ProcessingRegion.AppliedGuard.Should().Be(0);
        evidence
            .Contour.Statistics.ContextArea.Should()
            .BeGreaterThan(evidence.Contour.Statistics.FeatureArea);
        evidence.InputRole.Should().Be(CaptureProcessingInputRole.LiveSensor);
        evidence.OriginalQualification.Value.Qualified.Should().BeTrue();
        rig.Services.Counts.Should().Be((1, 1, 1));
    }
}
