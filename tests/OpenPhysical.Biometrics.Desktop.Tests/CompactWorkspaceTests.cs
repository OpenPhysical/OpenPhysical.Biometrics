// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CompactWorkspaceTests
{
    [AvaloniaTest]
    public async Task WindowStartsCompactAndStillAllowsTheMinimumWorkingSize()
    {
        var host = DesktopServices.CreateHost();
        var window = new MainWindow(
            host,
            host.Services.GetRequiredService<WorkspaceViewModel>(),
            host.Services.GetRequiredService<DesktopFilePicker>()
        );
        window.Width.Should().Be(1100);
        window.Height.Should().Be(760);
        window.MinWidth.Should().Be(800);
        window.MinHeight.Should().Be(600);
        await ((IAsyncDisposable)host).DisposeAsync();
    }

    [AvaloniaTest]
    public async Task RequiredEnrollmentActionsFitWithoutScrollingTheWholeWorkspace()
    {
        var host = DesktopServices.CreateHost();
        var workspace = host.Services.GetRequiredService<WorkspaceViewModel>();
        await workspace.Export.Enrollment.SelectSectionCommand.ExecuteAsync(
            workspace.Export.Enrollment.Sections.Single(section => section.Id == "fingerprints")
        );
        foreach (var size in new[] { new Size(800, 600), new Size(1100, 760), new Size(1280, 880) })
        {
            var view = new EnrollmentView { DataContext = workspace };
            var window = new Window
            {
                Width = size.Width,
                Height = size.Height,
                Content = view,
            };
            window.Show();
            AssertActionsFit(view);
            CountInstructions(view).Should().Be(1);
            window.Close();
        }
        await ((IAsyncDisposable)host).DisposeAsync();
    }

    private static bool AssertActionsFit(Control view)
    {
        var visible = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.IsEffectivelyVisible)
            .ToArray();
        foreach (
            var action in visible.Where(button =>
                button.Content is string label
                && label is "Capture now" or "Reset capture" or "Clear person" or "Settings"
            )
        )
        {
            var origin = action.TranslatePoint(default, view);
            origin.HasValue.Should().BeTrue();
            if (origin is not Point position)
                continue;
            position.X.Should().BeGreaterThanOrEqualTo(0);
            position.Y.Should().BeGreaterThanOrEqualTo(0);
            (position.X + action.Bounds.Width).Should().BeLessThanOrEqualTo(view.Bounds.Width + 1);
            (position.Y + action.Bounds.Height)
                .Should()
                .BeLessThanOrEqualTo(view.Bounds.Height + 1);
            action.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
        }
        visible.Should().Contain(button => Equals(button.Content, "Capture now"));
        return true;
    }

    [AvaloniaTest]
    public async Task CaptureAndCompareActionsFitAtAllSupportedWindowSizes()
    {
        var host = DesktopServices.CreateHost();
        var workspace = host.Services.GetRequiredService<WorkspaceViewModel>();
        await workspace.Export.Enrollment.SelectSectionCommand.ExecuteAsync(
            workspace.Export.Enrollment.Sections.Single(section => section.Id == "fingerprints")
        );
        foreach (var size in new[] { new Size(800, 600), new Size(1100, 760), new Size(1280, 880) })
        {
            var window = WorkspaceTestWindow.Create(host, workspace, size);
            var content = (Control)window.Content!;
            window.Show();
            content.DataContext.Should().BeSameAs(workspace);
            window.ClientSize.Should().Be(size);
            var tabs = content.GetVisualDescendants().OfType<TabControl>().First();
            foreach (var tab in new[] { 0, 1 })
            {
                tabs.SelectedIndex = tab;
                window.UpdateLayout();
                WorkspaceReviewImages.Save(window, size, tab.ToString());
                AssertActionsFit(content);
                CountInstructions(content).Should().Be(1);
            }
            window.Close();
        }
        await ((IAsyncDisposable)host).DisposeAsync();
    }

    private static int CountInstructions(Control view) =>
        view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Count(text => text.IsEffectivelyVisible && text.Text == "Place your finger");
}
