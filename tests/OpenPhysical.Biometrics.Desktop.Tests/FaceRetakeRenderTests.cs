// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

[TestFixture]
internal sealed class FaceRetakeRenderTests
{
    [AvaloniaTest]
    public async Task RejectedPortraitSeparatesFailureOverrideFromNormalAcceptance()
    {
        using var host = DesktopServices.CreateHost();
        await using var context = new PersonWorkspaceContext(
            new FaceTestCameraProvider { FixtureRotation = 15 }
        );
        context.Person.Name = "Example person";
        await context.Enrollment.ActivateAsync(true);
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        await context.Face.Actions.Capture.ExecuteAsync(default);
        var workspace = new WorkspaceViewModel(
            context.Fingerprints.Capture,
            context.Export,
            context.Fingerprints.Inspection,
            host.Services.GetRequiredService<SettingsViewModel>()
        );
        RenderSizes(host, workspace, false);
        await context.Face.Actions.Override.ExecuteAsync(default);
        RenderSizes(host, workspace, true);
    }

    private static bool RenderSizes(
        Microsoft.Extensions.Hosting.IHost host,
        WorkspaceViewModel workspace,
        bool overridden
    )
    {
        foreach (var size in new[] { new Size(800, 600), new Size(1100, 760), new Size(1280, 880) })
        {
            var window = WorkspaceTestWindow.Create(host, workspace, size);
            window.Show();
            var view = window.GetVisualDescendants().OfType<FaceCaptureView>().Single();
            AssertActions(view, window, overridden);
            WorkspaceReviewImages.Save(window, size, overridden ? "face-override" : "face-retake");
            window.Close();
        }
        return true;
    }

    private static bool AssertActions(FaceCaptureView view, Window window, bool overridden)
    {
        var actions = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.IsEffectivelyVisible)
            .ToArray();
        var retake = actions.Single(button => Equals(button.Content, "Retake"));
        retake.IsEffectivelyEnabled.Should().BeTrue();
        retake.Classes.Contains("Flat").Should().Be(overridden);
        actions
            .Count(button => Equals(button.Content, "Override failure"))
            .Should()
            .Be(overridden ? 0 : 1);
        actions
            .Count(button => Equals(button.Content, "Accept face"))
            .Should()
            .Be(overridden ? 1 : 0);
        AssertFailureLine(view);
        foreach (var button in actions)
        {
            button.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
            var position = button.TranslatePoint(default, window)!.Value;
            (position.X + button.Bounds.Width)
                .Should()
                .BeLessThanOrEqualTo(window.Bounds.Width + 1);
            (position.Y + button.Bounds.Height)
                .Should()
                .BeLessThanOrEqualTo(window.Bounds.Height + 1);
        }
        return true;
    }

    private static bool AssertFailureLine(FaceCaptureView view)
    {
        var failure = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(text =>
                text.IsEffectivelyVisible
                && text.Text is string message
                && message.StartsWith(
                    "Photo failed the following tests: ",
                    StringComparison.Ordinal
                )
            );
        ((ISolidColorBrush)failure.Foreground!).Color.Should().Be(Color.Parse("#B3261E"));
        return true;
    }
}
