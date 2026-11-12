// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureDarknessDefaultsTests
{
    [AvaloniaTest]
    [TestCase(220, true)]
    [TestCase(123, false)]
    public async Task PreviewRequestsTheDisplayedExposureFromTheScanner(
        int exposure,
        bool automatic
    )
    {
        var driver = new ControlledCaptureDriver();
        var dispatcher = new DesktopPreviewDispatcher();
        await using var coordinator = new CaptureCoordinator(
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
        view.Exposure = exposure;
        view.AutomaticExposure = automatic;
        (await view.StartAsync(CancellationToken.None)).Should().Succeed();
        var first = await driver.NextAsync();
        first.Settings.Exposure.Should().Be((byte)exposure);
        (await view.StopAsync()).Should().Succeed();
    }
}
