// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Facial;

[TestFixture]
internal sealed class PhotoAutoCaptureViewTests
{
    [AvaloniaTest]
    public async Task LivePhotoSwitchUsesTheSavedPhotoPreferenceWithoutChangingFingerprints()
    {
        using var host = DesktopServices.CreateHost();
        using var location = new SettingsTestFile();
        await using var context = new PersonWorkspaceContext();
        var settings = Settings(context, location);
        await settings.InitializeAsync();
        await context.Enrollment.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        var window = OpenWindow(host, context, settings);
        var toggle = Toggle(window, "Auto capture");
        toggle.IsEffectivelyVisible.Should().BeTrue();
        toggle.IsChecked.Should().BeFalse();
        toggle.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
        toggle.IsChecked = true;
        await settings.SaveCommand.ExecuteAsync(default);
        context.Face.AutoCapture.Should().BeTrue();
        context.Fingerprints.Capture.AutoCapture.Should().BeFalse();
        (await new SettingsStorage(location.Path).LoadAsync(CancellationToken.None))
            .Value.FaceAutoCapture.Should()
            .BeTrue();
        WorkspaceReviewImages.Save(window, new Size(800, 600), "photo-auto-capture");
        await context.Face.Actions.Capture.ExecuteAsync(default);
        toggle.IsEffectivelyVisible.Should().BeFalse();
        window.Close();
        await settings.StopAsync();
    }

    [AvaloniaTest]
    public async Task CapturedBackgroundToggleUpdatesTheFrozenPhotoAndDisappearsOnAcceptance()
    {
        using var host = DesktopServices.CreateHost();
        using var location = new SettingsTestFile();
        await using var context = new PersonWorkspaceContext();
        var settings = Settings(context, location);
        await settings.InitializeAsync();
        context.Person.Name = "Example person";
        await context.Face.ConfigureMouthPolicyAsync(PortraitMouthPolicy.Create(.15).Value);
        await context.Enrollment.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        var window = OpenWindow(host, context, settings);
        var toggle = Toggle(window, "Remove background");
        toggle.IsEffectivelyVisible.Should().BeFalse();
        await context.Face.Actions.Capture.ExecuteAsync(default);
        window.UpdateLayout();
        toggle.IsEffectivelyVisible.Should().BeTrue();
        toggle.IsChecked.Should().BeTrue();
        toggle.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
        var opened = context.Camera.OpenCount;
        toggle.IsChecked = false;
        await settings.SaveCommand.ExecuteAsync(default);
        context.Face.HasCapture.Should().BeTrue();
        context.Face.Details.Should().NotContain("Background replaced");
        context.Camera.OpenCount.Should().Be(opened);
        WorkspaceReviewImages.Save(window, new Size(800, 600), "photo-background-toggle");
        context.Face.ReviewConfirmed = true;
        await context.Face.AcceptCommand.ExecuteAsync(default);
        context.Face.Complete.Should().BeTrue();
        toggle.IsEffectivelyVisible.Should().BeFalse();
        context.Face.Accepted.Value.Portrait.Background.HasNoValue.Should().BeTrue();
        window.Close();
        await settings.StopAsync();
    }

    private static Window OpenWindow(
        IHost host,
        PersonWorkspaceContext context,
        SettingsViewModel settings
    )
    {
        var workspace = new WorkspaceViewModel(
            context.Fingerprints.Capture,
            context.Export,
            context.Fingerprints.Inspection,
            settings
        );
        var window = WorkspaceTestWindow.Create(host, workspace, new Size(800, 600));
        window.Show();
        return window;
    }

    private static CheckBox Toggle(Window window, string label) =>
        window
            .GetVisualDescendants()
            .OfType<FaceCaptureView>()
            .Single()
            .GetVisualDescendants()
            .OfType<CheckBox>()
            .Single(control => Equals(control.Content, label));

    private static SettingsViewModel Settings(
        PersonWorkspaceContext context,
        SettingsTestFile location
    ) =>
        new(
            new SettingsStorage(location.Path),
            async value =>
            {
                context.Fingerprints.Capture.AutoCapture = value.AutoCapture;
                var background = await context.Face.ConfigureBackgroundAsync(
                    value.FaceBackgroundReplacement
                );
                return background.IsFailure
                    ? background
                    : await context.Face.ConfigureAutoCaptureAsync(
                        value.FaceAutoCapture,
                        TimeSpan.FromSeconds(value.AutoCaptureDelaySeconds)
                    );
            },
            NullLogger<SettingsViewModel>.Instance
        );
}
