// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Imaging;

[TestFixture]
internal sealed class WorkspaceSourceTests
{
    [AvaloniaTest]
    public async Task CompareUsesScannerAndPauseStopsEverySourceRegardlessOfEnrollmentSelection()
    {
        await using var context = new PersonWorkspaceContext();
        var workspace = CreateWorkspace(context);
        (await workspace.ActivateAsync(true)).Should().Succeed();
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        context.Face.IsLive.Should().BeTrue();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        (await workspace.ActivateAsync(false)).Should().Succeed();
        context.Face.IsLive.Should().BeFalse();
        context.Fingerprints.Coordinator.IsLive.Should().BeTrue();
        await workspace.ClearPersonCommand.ExecuteAsync(default);
        context.Face.IsLive.Should().BeFalse();
        context.Fingerprints.Coordinator.IsLive.Should().BeTrue();
        (await workspace.PauseAsync()).Should().Succeed();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        (await workspace.ActivateAsync(true)).Should().Succeed();
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        context.Face.IsLive.Should().BeTrue();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        (await workspace.StopSourcesAsync()).Should().Succeed();
        (await workspace.ActivateAsync(false))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Capture.Closed");
        context.Face.IsLive.Should().BeFalse();
    }

    [AvaloniaTest]
    public async Task EnteringComparePreservesThePreparedFrozenProbe()
    {
        await using var context = new PersonWorkspaceContext();
        var workspace = CreateWorkspace(context);
        var captured = context.Fingerprints.NativeCapture();
        (await context.Fingerprints.PresentAsync(captured)).Should().Succeed();
        (await workspace.ActivateAsync(false)).Should().Succeed();
        context.Fingerprints.Capture.Captured.Value.Should().BeSameAs(captured);
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        await workspace.StopSourcesAsync();
    }

    [AvaloniaTest]
    public async Task ANewPauseDuringClearDoesNotReopenTheCameraAfterItsPendingOpenDrains()
    {
        await using var context = new PersonWorkspaceContext(true);
        var workspace = CreateWorkspace(context);
        var activating = workspace.ActivateAsync(true);
        await context.Camera.Opening.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var clearing = workspace.ClearPersonCommand.ExecuteAsync(default);
        var locked = workspace.PauseAsync();
        context.Camera.ReleaseOpen();
        await Task.WhenAll(activating, clearing, locked).WaitAsync(TimeSpan.FromSeconds(5));
        context.Camera.OpenCount.Should().Be(1);
        context.Face.IsLive.Should().BeFalse();
        context.Fingerprints.Coordinator.IsLive.Should().BeFalse();
        context.Camera.LastFrame.Value.Image.CopyRgbBytes().Should().Fail();
        await workspace.StopSourcesAsync();
    }

    private static WorkspaceViewModel CreateWorkspace(PersonWorkspaceContext context) =>
        new(
            context.Fingerprints.Capture,
            context.Export,
            context.Fingerprints.Inspection,
            new SettingsViewModel(
                new SettingsStorage("/private/tmp/unused-studio-settings.json"),
                _ => Task.FromResult(UnitResult.Success<DesktopError>()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
            )
        );
}
