// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class SecondFingerAutoCaptureTests
{
    [AvaloniaTest]
    public async Task InitialFingerCanAutoCaptureWithoutAnEmptySensorPrimer()
    {
        await using var rig = new LiveEnrollmentCaptureRig();
        rig.View.AutoCapture = true;
        await rig.View.StartAsync(CancellationToken.None);
        var pixels = ProcessingFrameFixtures.PublicFinger();
        var pending = await rig.Driver.NextAsync();
        pending.Complete(pixels).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        rig.View.CaptureCommand.CanExecute(default).Should().BeTrue();
        rig.View.Scene.HasValue.Should().BeTrue();
        pending = await HoldAsync(rig, pending, pixels, 500);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(500));
        pending.Complete(pixels).Should().BeTrue();
        var captured = await rig.NextCaptureAsync();
        await rig.View.CaptureCommand.ExecutionTask!;
        captured
            .Processing.Value.OriginalQualification.Value.ReferenceDigest.HasNoValue.Should()
            .BeTrue();
        captured
            .Processing.Value.OriginalQualification.Value.QualifiedAnchorDigest.HasNoValue.Should()
            .BeTrue();
        rig.View.CaptureGuidance.Should().Be("Captured");
    }

    [AvaloniaTest]
    public async Task AcceptedFirstCaptureRearmsSecondWhileTheNewExposureReferenceWarms()
    {
        await using var rig = new LiveEnrollmentCaptureRig();
        var first = await CaptureFirstAsync(rig);
        await rig.Export.Enrollment.AcceptCommand.ExecuteAsync(default);
        rig.Export.Enrollment.Progress.Should().Be("1 of 2 fingerprints accepted");
        rig.View.Captured.HasNoValue.Should().BeTrue();
        var pending = await rig.Driver.NextAsync();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        pending = await KeepRemovalRequiredAsync(rig, pending, pixels);
        var empty = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (var index = 0; index < 2; index++)
        {
            if (index > 0)
                rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
            pending.Settings.Exposure.Should().Be(236);
            pending.Complete(empty).Should().BeTrue();
            pending = await rig.Driver.NextAsync();
            if (index == 0)
                rig.View.CaptureGuidance.Should().NotBe("Place your finger");
        }
        pending.Complete(pixels).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        rig.View.CaptureCommand.CanExecute(default).Should().BeTrue();
        pending = await HoldAsync(rig, pending, pixels, 500);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(500));
        pending.Complete(pixels).Should().BeTrue();
        var second = await rig.NextCaptureAsync();
        await rig.View.CaptureCommand.ExecutionTask!;
        second.Frame.Should().NotBeSameAs(first.Frame);
        second
            .Processing.Value.OriginalQualification.Value.ReferenceDigest.HasNoValue.Should()
            .BeTrue();
        await rig.Export.Enrollment.AcceptCommand.ExecuteAsync(default);
        rig.Export.Enrollment.Ready.Should().BeTrue();
        await rig.Export.Enrollment.Fingerprints.PrimarySlotCommand.ExecuteAsync(default);
        rig.View.Captured.Value.Should().BeSameAs(first);
    }

    private static async Task<ControlledCaptureRequest> KeepRemovalRequiredAsync(
        LiveEnrollmentCaptureRig rig,
        ControlledCaptureRequest pending,
        byte[] pixels
    )
    {
        var light = pixels.Select(value => (byte)(235 + value * 20 / 255)).ToArray();
        var residue = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        uint state = 19;
        for (var y = 210; y < 530; y++)
        for (var x = 280; x < 520; x++)
        {
            state = state * 1664525 + 1013904223;
            residue[y * 800 + x] = (byte)(90 + state % 151);
        }
        foreach (var rejected in new[] { light, residue })
        {
            pending.Complete(rejected).Should().BeTrue();
            pending = await rig.Driver.NextAsync();
            rig.View.Captured.HasNoValue.Should().BeTrue();
        }
        pending.Complete(pixels).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        rig.View.CaptureCommand.CanExecute(default).Should().BeFalse();
        rig.View.CaptureGuidance.Should()
            .Contain("Lift your finger")
            .And.Contain("If it is already off, clean the sensor");
        return pending;
    }

    private static async Task<CapturedFingerprint> CaptureFirstAsync(LiveEnrollmentCaptureRig rig)
    {
        rig.View.AutoCapture = true;
        rig.Export.Enrollment.Person.Name = "Test person";
        rig.Export.Enrollment.Person.Identifier = "P-101";
        rig.View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Original,
                CapturePolicySettings.Default
            )
            .Should()
            .Succeed();
        var pending = await rig.StartAsync();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        pending.Complete(pixels).Should().BeTrue();
        pending = await rig.Driver.NextAsync();
        rig.View.Exposure = 236;
        await rig.View.ExposureCommand.ExecuteAsync(default);
        pending = await HoldAsync(rig, pending, pixels, 100);
        pending.Settings.Exposure.Should().Be(236);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(900));
        pending.Complete(pixels).Should().BeTrue();
        var captured = await rig.NextCaptureAsync();
        await rig.View.CaptureCommand.ExecutionTask!;
        rig.View.CaptureGuidance.Should().Be("Captured");
        return captured;
    }

    private static async Task<ControlledCaptureRequest> HoldAsync(
        LiveEnrollmentCaptureRig rig,
        ControlledCaptureRequest pending,
        byte[] pixels,
        int milliseconds
    )
    {
        rig.Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
        pending.Complete(pixels).Should().BeTrue();
        return await rig.Driver.NextAsync();
    }
}
