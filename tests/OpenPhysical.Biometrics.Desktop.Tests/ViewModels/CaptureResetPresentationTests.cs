// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureResetPresentationTests
{
    [AvaloniaTest]
    public async Task ResetReplacesThePreviousCaptureStatusBeforeAnotherFrameArrives()
    {
        var driver = new ControlledCaptureDriver();
        var dispatcher = new DesktopPreviewDispatcher();
        var coordinator = new CaptureCoordinator(
            driver.Registry,
            dispatcher,
            TimeProvider.System,
            NullLogger<CaptureCoordinator>.Instance
        );
        var view = new CaptureViewModel(
            coordinator,
            new ControlledCaptureProcessor(),
            dispatcher,
            TimeProvider.System
        );
        (await view.ReviewAcceptedAsync(StorageFixtures.Capture())).Should().Succeed();
        var oldStatus = view.Status;
        oldStatus.Should().Contain("class");
        await view.ResetCaptureCommand.ExecuteAsync(default);
        view.Status.Should().NotBe(oldStatus);
        view.Status.Should().Be("Waiting for a fingerprint.");
        view.Captured.HasNoValue.Should().BeTrue();
        view.Scene.HasNoValue.Should().BeTrue();
        await view.StopAsync();
    }
}
