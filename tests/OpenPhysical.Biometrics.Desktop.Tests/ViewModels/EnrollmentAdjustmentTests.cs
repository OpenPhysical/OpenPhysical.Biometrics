// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class EnrollmentAdjustmentTests
{
    [AvaloniaTest]
    [TestCase(800, 600)]
    [TestCase(1100, 760)]
    [TestCase(1280, 880)]
    public async Task CancelRestoresTheSecondPendingCaptureWithoutAcceptingIt(int width, int height)
    {
        await using var rig = await EnrollmentAdjustmentContext.Create();
        await using var surface = new EnrollmentAdjustmentSurface(rig, new Size(width, height));
        rig.Editor.Controls.Contrast.Should()
            .Be(rig.Pending.Processing.Value.Artifact.Policy.Contrast);
        rig.Counts.Should().Be(rig.EntryCounts);
        rig.Fingers.Progress.Should().Be(rig.PendingProgress);
        rig.Fingers.ActiveAccepted.Should().BeFalse();
        rig.Export.SaveCommand.CanExecute(default).Should().BeFalse();
        rig.Fingers.PrimarySlotCommand.CanExecute(default).Should().BeFalse();
        await rig.PreviewAsync(() => rig.Editor.Controls.Contrast = 30);
        rig.Editor.ChangeSummary.Should().Contain("Contrast 0 → 30");
        surface.AssertEditorFits();
        var counts = rig.Counts;
        await rig.Editor.CancelCommand.ExecuteAsync(default);
        rig.Counts.Should().Be(counts);
        rig.Capture.Captured.Value.Should().BeSameAs(rig.Pending);
        rig.Capture.Scene.Value.GrayPixels.Should()
            .Equal(rig.Pending.Processing.Value.Artifact.Pixels);
        rig.Editor.IsOpen.Should().BeFalse();
        surface.AssertReviewActions(true);
        await rig.AcceptPendingAsync(rig.Pending);
        surface.AssertReviewActions(false);
    }

    [AvaloniaTest]
    [TestCase(false)]
    [TestCase(true)]
    public async Task EitherPendingFingerCanApplyChangesBeforeASeparateAccept(bool second)
    {
        await using var rig = await EnrollmentAdjustmentContext.Create(
            second: second,
            modified: true,
            open: false
        );
        await using var surface = new EnrollmentAdjustmentSurface(rig, new Size(1100, 760));
        surface.AssertReviewActions(true);
        await rig.Fingers.AdjustCaptureCommand.ExecuteAsync(default);
        await rig.PreviewAsync(() => rig.Editor.Controls.OriginalCommand.Execute(default));
        var counts = rig.Counts;
        await rig.PreviewAsync(() => rig.Editor.Controls.Restore(ImageAdjustmentSettings.Default));
        await rig.PreviewAsync(() => rig.Editor.Controls.OriginalCommand.Execute(default));
        rig.Counts.Should().Be(counts);
        var changed = rig.Capture.Captured.Value;
        await rig.Editor.ApplyCommand.ExecuteAsync(default);
        rig.Editor.IsOpen.Should().BeFalse();
        rig.Fingers.Progress.Should().Be(rig.PendingProgress);
        rig.Fingers.ActiveAccepted.Should().BeFalse();
        rig.Capture.Captured.Value.Should().BeSameAs(changed);
        changed.Frame.Should().BeSameAs(rig.Pending.Frame);
        surface.AssertReviewActions(true);
        await rig.AcceptPendingAsync(changed);
        surface.AssertReviewActions(false);
    }

    [AvaloniaTest]
    public async Task FailedPendingDraftAndInFlightCancelKeepTheOtherAcceptedFinger()
    {
        await using var rig = await EnrollmentAdjustmentContext.Create(recorded: true);
        var counts = rig.Counts;
        await rig.PreviewAsync(() => rig.Editor.Controls.Brightness = 8);
        rig.Editor.Error.Should().NotBeEmpty();
        rig.Editor.ApplyCommand.CanExecute(default).Should().BeFalse();
        rig.Counts.Should().Be(counts);
        rig.Fingers.Progress.Should().Be(rig.PendingProgress);
        await rig.Editor.CancelCommand.ExecuteAsync(default);
        rig.Capture.Captured.Value.Should().BeSameAs(rig.Pending);
        await rig.Fingers.AdjustCaptureCommand.ExecuteAsync(default);
        rig.Editor.Controls.Contrast = 31;
        await rig.Editor.CancelCommand.ExecuteAsync(default);
        rig.Editor.IsOpen.Should().BeFalse();
        rig.Capture.Captured.Value.Should().BeSameAs(rig.Pending);
        rig.Fingers.ActiveAccepted.Should().BeFalse();
        await rig.Fingers.PrimarySlotCommand.ExecuteAsync(default);
        rig.Capture.Captured.Value.Should().BeSameAs(rig.First);
        rig.Fingers.CanAdjustCapture.Should().BeFalse();
        await rig.Fingers.SecondarySlotCommand.ExecuteAsync(default);
        await rig.Capture.ReviewAcceptedAsync(rig.Pending);
        await rig.Fingers.AdjustCaptureCommand.ExecuteAsync(default);
        rig.Editor.IsOpen.Should().BeTrue();
        rig.Editor.Controls.Brightness = 19;
        rig.Editor.IsBusy.Should().BeTrue();
        await rig.Export.ClearPersonCommand.ExecuteAsync(default);
        rig.Editor.IsOpen.Should().BeFalse();
        rig.Capture.Scene.HasNoValue.Should().BeTrue();
        rig.Fingers.Progress.Should().Be("0 of 2 fingerprints accepted");
    }
}
