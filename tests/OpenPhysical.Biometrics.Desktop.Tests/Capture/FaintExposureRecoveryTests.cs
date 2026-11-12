// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FaintExposureRecoveryTests
{
    [AvaloniaTest]
    public async Task SpatiallyCorroboratedFaintContactCanImproveAcquisitionWithoutRunningNist()
    {
        await using var rig = new ProcessingCaptureRig();
        rig.View.AutomaticExposure = true;
        var request = await rig.StartAsync();
        var initial = request.Settings.Exposure;
        request.Complete(Ridges(225, 245)).Should().BeTrue();
        var recovering = await rig.Driver.NextAsync();
        rig.View.CaptureCommand.CanExecute(default).Should().BeFalse();
        rig.View.Scene.HasValue.Should().BeTrue();
        rig.View.LiveFrame.Value.Ready.Should().BeFalse();
        rig.View.LiveFrame.Value.WholeContactProof.Value.ExposureReady.Should().BeTrue();
        rig.View.LiveFrame.Value.WholeContactProof.Value.Qualified.Should().BeFalse();
        rig.Services.Counts.Should().Be((0, 0, 0));
        rig.View.CaptureGuidance.Should().Contain("Adjusting the scanner");
        recovering.Settings.Exposure.Should().BeGreaterThan(initial);
        ((int)recovering.Settings.Exposure).Should().BeLessThanOrEqualTo(initial + 16);
        var valid = Ridges(65, 165);
        recovering.Complete(valid).Should().BeTrue();
        _ = await rig.Driver.NextAsync();
        rig.View.LiveFrame.Value.Frame.Pixels.Should().Equal(valid);
        rig.View.CaptureCommand.CanExecute(default).Should().BeTrue();
        rig.Services.Counts.Should().Be((0, 0, 0));
    }

    [AvaloniaTest]
    public async Task FaintContactWithoutAnEmptyReferenceCanCorrectExposureWithoutCapture()
    {
        await using var rig = new ProcessingCaptureRig();
        rig.View.AutomaticExposure = true;
        await rig.View.StartAsync(CancellationToken.None);
        var request = await rig.Driver.NextAsync();
        var initial = request.Settings.Exposure;
        request.Complete(Ridges(225, 245)).Should().BeTrue();
        var pending = await rig.Driver.NextAsync();
        pending.Settings.Exposure.Should().BeGreaterThan(initial);
        ((int)pending.Settings.Exposure).Should().BeLessThanOrEqualTo(initial + 16);
        rig.View.CaptureGuidance.Should().Contain("Adjusting the scanner");
        rig.View.Scene.HasValue.Should().BeTrue();
        rig.View.LiveFrame.Value.WholeContactProof.Value.Qualified.Should().BeFalse();
        rig.View.CaptureCommand.CanExecute(default).Should().BeFalse();
        rig.Services.Counts.Should().Be((0, 0, 0));
    }

    [AvaloniaTest]
    public async Task MultipleContactsAndRandomResidueCannotDriveExposure()
    {
        foreach (var pixels in new[] { Multiple(), RandomResidue() })
        {
            await using var rig = new ProcessingCaptureRig();
            rig.View.AutomaticExposure = true;
            var request = await rig.StartAsync();
            var initial = request.Settings.Exposure;
            request.Complete(pixels).Should().BeTrue();
            var pending = await rig.Driver.NextAsync();
            pending.Settings.Exposure.Should().Be(initial);
            rig.View.LiveFrame.Value.Frame.Pixels.Should().Equal(pixels);
            rig.View.CaptureCommand.CanExecute(default).Should().BeFalse();
            rig.View.CaptureGuidance.Should().NotContain("Adjusting the scanner");
            rig.Services.Counts.Should().Be((0, 0, 0));
        }
    }

    private static byte[] Multiple()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (var y = 210; y < 530; y++)
        for (var x = 200; x < 600; x++)
            if (x < 320 || x >= 480)
                pixels[y * 800 + x] = x % 8 < 4 ? (byte)225 : (byte)245;
        return pixels;
    }

    private static byte[] RandomResidue()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        uint state = 123456;
        for (var y = 210; y < 530; y++)
        for (var x = 280; x < 520; x++)
        {
            state = state * 1664525 + 1013904223;
            pixels[y * 800 + x] = (byte)(90 + state % 151);
        }
        return pixels;
    }

    private static byte[] Ridges(byte low, byte high)
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (var y = 210; y < 530; y++)
        for (var x = 280; x < 520; x++)
            pixels[y * 800 + x] = x % 8 < 4 ? low : high;
        return pixels;
    }
}
