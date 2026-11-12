// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ProcessingFreezeTests
{
    [AvaloniaTest]
    public async Task ManualCaptureKeepsPreviewIntactAndPreparesOnlyAfterTheLateRawFrameDrains()
    {
        await using var rig = new ProcessingCaptureRig();
        (await rig.StartAsync()).Complete(ProcessingFrameFixtures.PublicFinger()).Should().BeTrue();
        var late = await rig.Driver.NextAsync();
        var displayed = rig.View.Scene.Value.GrayPixels;
        var artifact = rig.View.LiveFrame.Value.Processing.Value;
        rig.View.Scene.Value.Foreground.Should().BeEmpty();
        artifact.Policy.ClaheEnabled.Should().BeFalse();
        rig.Services.Counts.Should().Be((0, 0, 0));
        var capturing = rig.View.CaptureCommand.ExecuteAsync(default);
        rig.View.Scene.Value.GrayPixels.Should().Equal(displayed);
        rig.Services.Counts.Should().Be((0, 0, 0));
        late.Complete().Should().BeTrue();
        await capturing;
        var captured = rig.View.Captured.Value;
        captured.Processing.Value.Artifact.Should().NotBeSameAs(artifact);
        rig.Services.Engine.LastPixels.Should().Equal(captured.Processing.Value.Artifact.Pixels);
        rig.View.Scene.Value.GrayPixels.Should().Equal(captured.Processing.Value.Artifact.Pixels);
        captured.Frame.Should().BeSameAs(artifact.OriginalFrame);
        captured
            .Processing.Value.Artifact.ProcessedDigest.Should()
            .NotBe(captured.Processing.Value.Artifact.OriginalDigest);
        captured
            .Processing.Value.OriginalAssessment.ImageDigest.Should()
            .Be(captured.Processing.Value.Artifact.OriginalDigest);
        rig.Services.Counts.Should().Be((2, 2, 1));
    }
}
