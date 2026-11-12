// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class CaptureAdjustmentWorkTests
{
    [AvaloniaTest]
    public async Task LiveAdjustmentUpdatesPixelsWithoutCallingQualityOrExtraction()
    {
        await using var rig = new ProcessingCaptureRig();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        (await rig.StartAsync()).Complete(pixels).Should().BeTrue();
        await rig.Driver.NextAsync();
        var frame = rig.View.LiveFrame.Value.Frame;
        var preview = rig.View.LiveFrame.Value.Processing.Value;
        rig.View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Create(false, 7, 0).Value,
                CapturePolicySettings.Default
            )
            .Should()
            .Succeed();
        await CaptureWorkflowWait.UntilAsync(
            rig.View,
            () => rig.View.LiveFrame.Value.Processing.Value.Policy.Brightness == 7
        );
        rig.View.LiveFrame.Value.Frame.Should().BeSameAs(frame);
        rig.View.LiveFrame.Value.Processing.Value.Should().NotBeSameAs(preview);
        rig.View.Scene.Value.GrayPixels.Should().NotEqual(pixels);
        rig.View.Scene.Value.GrayPixels.Should()
            .Equal(rig.View.LiveFrame.Value.Processing.Value.Pixels);
        rig.Services.Counts.Should().Be((0, 0, 0));
    }

    [AvaloniaTest]
    public async Task ShutdownDrainsImageEditsAndCannotPublishALateCapture()
    {
        await using var rig = new ProcessingCaptureRig();
        (await rig.StartAsync()).Complete(ProcessingFrameFixtures.PublicFinger()).Should().BeTrue();
        await rig.Driver.NextAsync();
        rig.View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Create(false, 20, 0).Value,
                CapturePolicySettings.Default
            )
            .Should()
            .Succeed();
        (await rig.View.StopAsync()).Should().Succeed();
        rig.View.Captured.HasNoValue.Should().BeTrue();
        rig.View.Scene.HasNoValue.Should().BeTrue();
        rig.Services.Counts.Should().Be((0, 0, 0));
    }

    [AvaloniaTest]
    public async Task RapidFrozenEditsPublishOnlyTheLatestRevisionOfTheSameAcquisition()
    {
        await using var rig = new ProcessingCaptureRig();
        var original = await rig.CaptureAsync();
        var baseline = rig.Services.Counts;
        var completed = 0;
        rig.View.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(rig.View.Captured) && rig.View.Captured.HasValue)
                completed++;
        };
        for (var brightness = 1; brightness <= 10; brightness++)
            rig.View.ConfigureImageAdjustments(
                    ImageAdjustmentSettings.Create(false, brightness, 0).Value,
                    CapturePolicySettings.Default
                )
                .Should()
                .Succeed();
        await CaptureWorkflowWait.UntilAsync(rig.View, () => rig.View.Captured.HasValue);
        var capture = rig.View.Captured.Value;
        completed.Should().Be(1);
        capture.Processing.Value.Artifact.Policy.Brightness.Should().Be(10);
        capture.Frame.Should().BeSameAs(original.Frame);
        rig.View.Scene.Value.GrayPixels.Should().Equal(capture.Processing.Value.Artifact.Pixels);
        rig.Services.Counts.Should()
            .Be((baseline.Nfiq1 + 1, baseline.Nfiq2 + 1, baseline.Extractions + 1));
    }

    [AvaloniaTest]
    public async Task ContextOnlyRevisionReusesTheExactAssessmentAndUnfilteredExtraction()
    {
        await using var rig = new ProcessingCaptureRig();
        rig.Driver.Available = false;
        var original = OriginalCapture(rig);
        var before = rig.Services.Counts;
        rig.View.ConfigureImageAdjustments(ImageAdjustmentSettings.Original, ContextPolicy(64))
            .Should()
            .Succeed();
        (await rig.View.AdjustAcceptedAsync(original)).Should().Succeed();
        var revised = rig.View.Captured.Value;
        revised.Frame.Should().BeSameAs(original.Frame);
        revised
            .Processing.Value.Contour.ContextDigest.Should()
            .NotBe(original.Processing.Value.Contour.ContextDigest);
        revised
            .Processing.Value.Contour.FeatureDigest.Should()
            .Be(original.Processing.Value.Contour.FeatureDigest);
        revised.View.Minutiae.Should().Equal(original.View.Minutiae);
        revised
            .Processing.Value.OriginalAcquisitionPolicy.Value.Should()
            .BeSameAs(original.Processing.Value.OriginalAcquisitionPolicy.Value);
        rig.Services.Counts.Should().Be(before);
    }

    [AvaloniaTest]
    public async Task ReturningToOriginalReusesItsOwnedAssessmentAndExtraction()
    {
        await using var rig = new ProcessingCaptureRig();
        rig.Driver.Available = false;
        var original = OriginalCapture(rig);
        rig.Services.Counts.Should().Be((1, 1, 1));
        rig.View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Create(false, 0, 20).Value,
                CapturePolicySettings.Default
            )
            .Should()
            .Succeed();
        (await rig.View.AdjustAcceptedAsync(original)).Should().Succeed();
        rig.Services.Counts.Should().Be((2, 2, 2));
        rig.View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Original,
                CapturePolicySettings.Default
            )
            .Should()
            .Succeed();
        await CaptureWorkflowWait.UntilAsync(
            rig.View,
            () =>
                rig.View.Captured.HasValue
                && rig.View.Captured.Value.Processing.Value.Artifact.Policy.IsOriginal
        );
        var restored = rig.View.Captured.Value;
        restored.Frame.Should().BeSameAs(original.Frame);
        restored.Quality.Should().BeSameAs(original.Quality);
        restored.Nfiq2.Value.Should().BeSameAs(original.Nfiq2.Value);
        restored.View.Minutiae.Should().Equal(original.View.Minutiae);
        rig.View.Scene.Value.GrayPixels.Should().Equal(original.Frame.Pixels);
        rig.Services.Counts.Should().Be((2, 2, 2));
    }

    private static CapturedFingerprint OriginalCapture(ProcessingCaptureRig rig) =>
        new CaptureAcquisitionPolicy(CapturePolicySettings.Default, 1).ApplyTo(
            ProcessingContractFixtures
                .Capture(
                    rig.Services.Processor,
                    ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger()),
                    FingerPosition.RightIndex
                )
                .Value
        );

    private static CapturePolicySettings ContextPolicy(int margin)
    {
        var policy = CapturePolicySettings.Default;
        return CapturePolicySettings
            .Create(
                policy.Enhancement,
                policy.EvidenceOptions,
                policy.EmptySensorOptions,
                policy.MinimumNfiq1Gain,
                policy.MinimumNfiq2Gain,
                margin
            )
            .Value;
    }
}
