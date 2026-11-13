// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class StudioFeatureTests
{
    [AvaloniaTest]
    public async Task AFeatureTabPausesBothSourcesAndShutdownStopsTheFeature()
    {
        await using var context = new PersonWorkspaceContext();
        var feature = new StudioTestFeature();
        var coordinator = new StudioFeatureCoordinator(Workspace(context), [feature]);
        (await coordinator.SelectAsync(0)).Should().Succeed();
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        context.Face.IsLive.Should().BeTrue();
        (await coordinator.SelectAsync(2)).Should().Succeed();
        context.Face.IsLive.Should().BeFalse();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        feature.IsActive.Should().BeTrue();
        (await coordinator.SelectAsync(1)).Should().Succeed();
        feature.IsActive.Should().BeFalse();
        context.Fingerprints.Coordinator.IsLive.Should().BeTrue();
        (await coordinator.SelectAsync(2)).Should().Succeed();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        (await coordinator.StopAsync()).Should().Succeed();
        feature.Stopped.Should().BeTrue();
        (await coordinator.SelectAsync(0))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Workspace.Closed");
    }

    [AvaloniaTest]
    public async Task SwitchingToAFeatureWhileCameraOpenIsPendingDrainsTheCamera()
    {
        await using var context = new PersonWorkspaceContext(true);
        var feature = new StudioTestFeature();
        var coordinator = new StudioFeatureCoordinator(Workspace(context), [feature]);
        var camera = coordinator.SelectAsync(0);
        await context.Camera.Opening.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var signing = coordinator.SelectAsync(2);
        context.Camera.ReleaseOpen();
        await Task.WhenAll(camera, signing).WaitAsync(TimeSpan.FromSeconds(5));
        feature.IsActive.Should().BeTrue();
        context.Face.IsLive.Should().BeFalse();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        context.Camera.OpenCount.Should().Be(1);
        await coordinator.StopAsync();
    }

    [AvaloniaTest]
    public async Task PrivacySuspensionCancelsAndDrainsPendingFeatureActivation()
    {
        await using var context = new PersonWorkspaceContext();
        var feature = new StudioTestFeature(true);
        var coordinator = new StudioFeatureCoordinator(Workspace(context), [feature]);
        using var privacy = new BiometricPrivacySession(
            context.Protection,
            coordinator.PauseAsync,
            NullLogger<BiometricPrivacySession>.Instance
        );
        var activating = coordinator.SelectAsync(2);
        await feature.Activating.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await privacy.SuspendAsync().WaitAsync(TimeSpan.FromSeconds(3));
        (await activating).Should().Succeed();
        feature.IsActive.Should().BeFalse();
        context.Protection.IsUnlocked.Should().BeFalse();
        context.Face.IsLive.Should().BeFalse();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        await coordinator.StopAsync();
    }

    [AvaloniaTest]
    public async Task ClearPersonCancelsTheActiveFeatureBeforeDiscardingIdentity()
    {
        await using var context = new PersonWorkspaceContext();
        context.Person.Name = "Public test person";
        var feature = new StudioTestFeature();
        var coordinator = new StudioFeatureCoordinator(Workspace(context), [feature]);
        (await coordinator.SelectAsync(2)).Should().Succeed();
        await coordinator.ClearPersonCommand.ExecuteAsync(default);
        feature.IsActive.Should().BeFalse();
        context.Person.Name.Should().BeEmpty();
        context.Face.IsLive.Should().BeFalse();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        await coordinator.StopAsync();
    }

    [AvaloniaTest]
    public async Task PrivacyPauseDuringClearNeverResumesAnEarlierCameraActivation()
    {
        await using var context = new PersonWorkspaceContext(true);
        var coordinator = new StudioFeatureCoordinator(Workspace(context), []);
        var activating = coordinator.SelectAsync(0);
        await context.Camera.Opening.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var clearing = coordinator.ClearPersonCommand.ExecuteAsync(default);
        var locked = coordinator.PauseAsync();
        context.Camera.ReleaseOpen();
        await Task.WhenAll(activating, clearing, locked).WaitAsync(TimeSpan.FromSeconds(5));
        context.Camera.OpenCount.Should().Be(1);
        context.Face.IsLive.Should().BeFalse();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        await coordinator.StopAsync();
    }

    private static WorkspaceViewModel Workspace(PersonWorkspaceContext context) =>
        new(
            context.Fingerprints.Capture,
            context.Export,
            context.Fingerprints.Inspection,
            new SettingsViewModel(
                new SettingsStorage("/private/tmp/unused-feature-settings.json"),
                _ => Task.FromResult(UnitResult.Success<DesktopError>()),
                NullLogger<SettingsViewModel>.Instance
            )
        );
}
