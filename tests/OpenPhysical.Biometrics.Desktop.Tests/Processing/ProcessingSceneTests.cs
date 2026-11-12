// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class ProcessingSceneTests
{
    [AvaloniaTest]
    public async Task CaptureAndComparePresentExactPixelsAndOnlyTheirIntendedFeatures()
    {
        await using var rig = new ProcessingCaptureRig();
        var capture = ProcessingStorageFixtures.Capture();
        var artifact = capture.Processing.Value.Artifact;
        var featureMask = capture.Processing.Value.Contour.FeatureMask;
        (await rig.View.ReviewAcceptedAsync(capture)).Should().Succeed();
        var inspection = new InspectionViewModel(
            new TemplateStorage(),
            new DesktopFilePicker(),
            rig.View,
            new TemplateComparison(new FingerprintComparisonService(rig.Services.Engine)),
            new CredentialReaderService(
                new ScriptedCredentialTransport(new ScriptedCredentialCard())
            )
        );
        var review = rig.View.Scene.Value;
        var probe = inspection.ProbeScene.Value;
        review.GrayPixels.Should().Equal(artifact.Pixels);
        probe.GrayPixels.Should().Equal(artifact.Pixels);
        review.Minutiae.Should().Equal(capture.View.Minutiae);
        probe.Minutiae.Should().BeEmpty();
        artifact.Pixels.Should().NotEqual(capture.Frame.Pixels);
        SceneGeometryExpectations
            .OutlineBounds(review)
            .Should()
            .Be(SceneGeometryExpectations.MaskBounds(featureMask, artifact.Width));
        featureMask.Should().NotEqual(artifact.Mask.Retained);
        review
            .Foreground.Should()
            .OnlyContain(edge =>
                edge.X1 >= 0
                && edge.X2 <= artifact.Width
                && edge.Y1 >= 0
                && edge.Y2 <= artifact.Height
            );
        rig.Services.Counts.Should().Be((0, 0, 0));
        (await inspection.StopAsync()).Should().Succeed();
    }

    [AvaloniaTest]
    public void DetailsShowExactImageAndMaskRolesAndClosingReleasesTheScene()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var details = new CaptureProcessingDetailsViewModel();
        details.UpdateCapture(capture).Should().Succeed();
        details.SetOpen(true).Should().Succeed();
        details.Scene.Value.GrayPixels.Should().Equal(capture.Processing.Value.Artifact.Pixels);
        details.Selected = "Original";
        details.Scene.Value.GrayPixels.Should().Equal(capture.Frame.Pixels);
        var evidence = capture.Processing.Value;
        var contour = evidence.Contour;
        var stages = new[]
        {
            ("Measured contact", contour.ContactMask),
            ("Filled holes", contour.FilledHoleMask),
            ("Feature area", contour.FeatureMask),
            ("Selected ridge support", contour.SupportMask),
            ("Uncertain area", contour.UncertainMask),
            ("Image context", contour.ContextMask),
            ("Guarded context", evidence.ProcessingRegion.GuardedContextMask),
            ("Eligible features", evidence.Artifact.Mask.Inner),
        };
        foreach (var (stage, mask) in stages)
        {
            details.Selected = stage;
            details
                .Scene.Value.GrayPixels.Should()
                .Equal(mask.Select(value => value == 0 ? (byte)255 : (byte)0));
        }
        details.SetOpen(false).Should().Succeed();
        details.Scene.HasNoValue.Should().BeTrue();
        details.Available.Should().BeTrue();
    }
}
