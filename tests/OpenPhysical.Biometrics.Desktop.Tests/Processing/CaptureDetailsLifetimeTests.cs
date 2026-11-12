// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;

[TestFixture]
internal sealed class CaptureDetailsLifetimeTests
{
    [AvaloniaTest]
    public async Task AcceptedCaptureSurvivesDisconnectAndShutdownReleasesItsDetails()
    {
        await using var rig = new ProcessingCaptureRig();
        var capture = ProcessingStorageFixtures.Capture();
        (await rig.View.ReviewAcceptedAsync(capture)).Should().Succeed();
        var details = rig.View.ProcessingDetails;
        details.SetOpen(true).Should().Succeed();
        var scene = details.Scene.Value;
        (
            await rig.Dispatcher.ReportAsync(
                new(CaptureErrorCodes.DeviceUnavailable, "Scanner disconnected.")
            )
        )
            .Should()
            .Succeed();
        rig.View.Captured.Value.Should().BeSameAs(capture);
        details.Scene.Value.Should().BeSameAs(scene);
        details.Available.Should().BeTrue();
        (await rig.View.StopAsync()).Should().Succeed();
        details.Available.Should().BeFalse();
        details.Scene.HasNoValue.Should().BeTrue();
    }

    [AvaloniaTest]
    public void UnrelatedProcessingEvidenceCannotBeDisplayedAsAnotherAcquisition()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var details = new CaptureProcessingDetailsViewModel();
        details.UpdateCapture(capture).Should().Succeed();
        details.SetOpen(true).Should().Succeed();
        var changed = capture with
        {
            Frame = ProcessingFrameFixtures.Frame(
                Enumerable.Repeat((byte)255, 800 * 750).ToArray()
            ),
        };
        details
            .UpdateCapture(changed)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Capture.DetailsSourceMismatch");
        details.Available.Should().BeFalse();
        details.Scene.HasNoValue.Should().BeTrue();
    }
}
