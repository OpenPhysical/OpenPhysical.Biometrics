// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureViewportTests
{
    [AvaloniaTest]
    [TestCase(420, 360)]
    [TestCase(260, 440)]
    [TestCase(680, 240)]
    public async Task NewCaptureFitsTheWholeFingerAfterManualZoomAndPan(int width, int height)
    {
        await using var rig = await CaptureViewportSurface.Create(new Size(width, height));
        var initial = rig.Outline();
        var image = rig.ImageArea;
        initial.Left.Should().BeGreaterThan(image.Left + 3);
        initial.Top.Should().BeGreaterThan(image.Top + 3);
        initial.Right.Should().BeLessThan(image.Right - 3);
        initial.Bottom.Should().BeLessThan(image.Bottom - 3);
        var source = rig.Capture.Scene.Value;
        var fullScale = Math.Min(image.Width / source.Width, image.Height / source.Height);
        var contact = rig.Source.Processing.Value.Contour.FeatureMask;
        var indices = Enumerable.Range(0, contact.Length).Where(i => contact[i] != 0).ToArray();
        var fullWidth =
            (indices.Max(i => i % source.Width) - indices.Min(i => i % source.Width)) * fullScale;
        initial.Width.Should().BeGreaterThan(fullWidth * 1.05);
        rig.Surface.Press("Zoom in");
        rig.Surface.Press("Zoom in");
        rig.Pan();
        (await rig.Capture.ReviewAcceptedAsync(HullStorageFixtures.Capture())).Should().Succeed();
        AssertSame(rig.Outline(), initial);
        rig.Capture.Scene.Value.GrayPixels.Should()
            .Equal(rig.Capture.Captured.Value.Processing.Value.Artifact.Pixels);
        WorkspaceReviewImages.Save(rig.Surface.Window, new Size(width, height), "context-auto-fit");
        rig.Surface.View.Invert = true;
        WorkspaceReviewImages.Save(
            rig.Surface.Window,
            new Size(width, height),
            "context-auto-fit-invert"
        );
    }

    [AvaloniaTest]
    public async Task SameSourceReviewKeepsManualViewportButClearingResetsIt()
    {
        await using var rig = await CaptureViewportSurface.Create(new Size(420, 360));
        var fitted = rig.Outline();
        rig.Surface.Press("Zoom in");
        rig.Pan();
        var manual = rig.Outline();
        (await rig.Capture.ReviewAcceptedAsync(rig.Source)).Should().Succeed();
        rig.Surface.View.ShowArea = false;
        rig.Surface.View.ShowArea = true;
        AssertSame(rig.Outline(), manual);
        WorkspaceReviewImages.Save(
            rig.Surface.Window,
            new Size(420, 360),
            "manual-review-preserved"
        );
        rig.Surface.View.Scene = Maybe<FingerprintScene>.None;
        rig.Surface.View.Scene = rig.Capture.Scene;
        AssertSame(rig.Outline(), fitted);
        WorkspaceReviewImages.Save(
            rig.Surface.Window,
            new Size(420, 360),
            "cleared-viewport-reset"
        );
    }

    [AvaloniaTest]
    public async Task FitUsesTheCurrentContextAfterASameSourcePolicyEdit()
    {
        await using var rig = await CaptureViewportSurface.Create(new Size(420, 360));
        var before = rig.Outline();
        var revision = CaptureViewportRevision.MoreContext(rig.Source);
        (await rig.Capture.ReviewAcceptedAsync(revision)).Should().Succeed();
        rig.Surface.Press("Fit image");
        rig.Outline().Width.Should().BeLessThan(before.Width - 5);
        WorkspaceReviewImages.Save(rig.Surface.Window, new Size(420, 360), "edited-context-fit");
        revision
            .Processing.Value.Artifact.Pixels.Should()
            .Equal(rig.Source.Processing.Value.Artifact.Pixels);
    }

    private static bool AssertSame(Rect actual, Rect expected)
    {
        actual.X.Should().BeApproximately(expected.X, 1);
        actual.Y.Should().BeApproximately(expected.Y, 1);
        actual.Width.Should().BeApproximately(expected.Width, 1);
        actual.Height.Should().BeApproximately(expected.Height, 1);
        return true;
    }
}
