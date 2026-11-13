// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

[TestFixture]
internal sealed class FaceWorkspaceRenderTests
{
    [AvaloniaTest]
    public async Task FacialReviewKeepsActionsReachableAtSupportedSizes()
    {
        using var host = DesktopServices.CreateHost();
        await using var context = new PersonWorkspaceContext();
        context.Person.Name = "Example person";
        await context.PrepareFaceAsync();
        var workspace = new WorkspaceViewModel(
            context.Fingerprints.Capture,
            context.Export,
            context.Fingerprints.Inspection,
            host.Services.GetRequiredService<SettingsViewModel>()
        );
        RenderSizes(host, workspace, "face-review", "Accept face");
    }

    private static bool RenderSizes(
        Microsoft.Extensions.Hosting.IHost host,
        WorkspaceViewModel workspace,
        string label,
        string action
    )
    {
        foreach (var size in new[] { new Size(800, 600), new Size(1100, 760), new Size(1280, 880) })
        {
            var window = WorkspaceTestWindow.Create(host, workspace, size);
            window.Show();
            var view = window.GetVisualDescendants().OfType<FaceCaptureView>().Single();
            view.GetVisualDescendants().OfType<TextBox>().Should().BeEmpty();
            AssertActionsFit(view, window, action);
            AssertToolbarFits(window);
            WorkspaceReviewImages.Save(window, size, label);
            window.Close();
        }
        return true;
    }

    private static bool AssertToolbarFits(Window window)
    {
        var buttons = window
            .GetVisualDescendants()
            .OfType<Button>()
            .Where(button =>
                button.IsEffectivelyVisible
                && button.Content is string text
                && text is "Settings" or "Clear person" or "Open package" or "Image adjustments"
            )
            .ToArray();
        foreach (var button in buttons)
        {
            var position = button.TranslatePoint(default, window)!.Value;
            position.X.Should().BeGreaterThanOrEqualTo(0);
            (position.X + button.Bounds.Width)
                .Should()
                .BeLessThanOrEqualTo(window.Bounds.Width + 1);
            button.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
        }
        return true;
    }

    private static bool AssertActionsFit(FaceCaptureView view, Window window, string label)
    {
        foreach (var canvas in view.GetVisualDescendants().OfType<FaceImageCanvas>())
        {
            var boundary = canvas.GetVisualAncestors().OfType<Border>().First();
            canvas.Bounds.Height.Should().BeLessThanOrEqualTo(boundary.Bounds.Height);
        }
        var buttons = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.IsEffectivelyVisible)
            .ToArray();
        buttons.Should().ContainSingle(button => Equals(button.Content, label));
        foreach (var button in buttons)
        {
            button.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
            var position = button.TranslatePoint(default, window)!.Value;
            position.X.Should().BeGreaterThanOrEqualTo(0);
            position.Y.Should().BeGreaterThanOrEqualTo(0);
            (position.X + button.Bounds.Width)
                .Should()
                .BeLessThanOrEqualTo(window.Bounds.Width + 1);
            (position.Y + button.Bounds.Height)
                .Should()
                .BeLessThanOrEqualTo(window.Bounds.Height + 1);
        }
        return true;
    }
}
