// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

[TestFixture]
internal sealed class ImageAdjustmentPanelTests
{
    [AvaloniaTest]
    public async Task ActualToolbarFlyoutKeepsLabelsAndTouchTargetsAtEveryWorkingSize()
    {
        var host = DesktopServices.CreateHost();
        try
        {
            var workspace = host.Services.GetRequiredService<WorkspaceViewModel>();
            foreach (
                var size in new[] { new Size(800, 600), new Size(1100, 760), new Size(1280, 880) }
            )
            {
                var window = WorkspaceTestWindow.Create(host, workspace, size);
                window.Show();
                var button = window
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(value => Equals(value.Content, "Image adjustments"));
                var flyout = button.Flyout.Should().BeOfType<Flyout>().Which;
                flyout.ShowAt(button);
                Dispatcher.UIThread.RunJobs();
                try
                {
                    var panel = flyout.Content.Should().BeOfType<ImageAdjustmentPanel>().Which;
                    panel.DataContext.Should().BeSameAs(workspace.Settings);
                    AssertControls(panel, workspace.Settings);
                    AssertVisible(panel, size);
                    SaveReview(panel, size);
                }
                finally
                {
                    flyout.Hide();
                    window.Close();
                }
            }
        }
        finally
        {
            await ((IAsyncDisposable)host).DisposeAsync();
        }
    }

    private static bool AssertControls(ImageAdjustmentPanel panel, SettingsViewModel settings)
    {
        var controls = panel.GetVisualDescendants().OfType<Control>().ToArray();
        var sliders = controls.OfType<Slider>().ToArray();
        sliders.Select(AutomationProperties.GetName).Should().Equal("Brightness", "Contrast");
        sliders[0].Minimum.Should().Be(-100);
        sliders[0].Maximum.Should().Be(100);
        sliders[1].Minimum.Should().Be(0);
        sliders[1].Maximum.Should().Be(100);
        AssertKeyboard(sliders[0], settings);
        var buttons = OperatorButtons(panel);
        buttons
            .Single(value => Equals(value.Content, "Original"))
            .Command.Should()
            .BeSameAs(settings.ImageAdjustments.OriginalCommand);
        buttons
            .Single(value => Equals(value.Content, "Reset"))
            .Command.Should()
            .BeSameAs(settings.ImageAdjustments.ResetCommand);
        buttons
            .Single(value => Equals(value.Content, "Save"))
            .Command.Should()
            .BeSameAs(settings.SaveCommand);
        foreach (var control in buttons.Cast<Control>().Concat(sliders))
        {
            control.Bounds.Width.Should().BeGreaterThanOrEqualTo(48);
            control.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
        }
        return true;
    }

    private static bool AssertKeyboard(Slider slider, SettingsViewModel settings)
    {
        var original = settings.ImageAdjustments.Brightness;
        slider.Focus(NavigationMethod.Tab).Should().BeTrue();
        slider.RaiseEvent(
            new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }
        );
        settings.ImageAdjustments.Brightness.Should().Be(original + 1);
        slider.Value = original;
        return true;
    }

    private static bool AssertVisible(ImageAdjustmentPanel panel, Size size)
    {
        panel.Bounds.Width.Should().BeGreaterThan(0).And.BeLessThan(size.Width);
        panel.Bounds.Height.Should().BeGreaterThan(0).And.BeLessThan(size.Height - 64);
        foreach (var action in OperatorButtons(panel))
        {
            var origin = action.TranslatePoint(default, panel)!.Value;
            origin.X.Should().BeGreaterThanOrEqualTo(0);
            origin.Y.Should().BeGreaterThanOrEqualTo(0);
            (origin.X + action.Bounds.Width).Should().BeLessThanOrEqualTo(panel.Bounds.Width + 1);
            (origin.Y + action.Bounds.Height).Should().BeLessThanOrEqualTo(panel.Bounds.Height + 1);
        }
        return true;
    }

    private static Button[] OperatorButtons(ImageAdjustmentPanel panel) =>
        panel
            .GetVisualDescendants()
            .OfType<Button>()
            .Where(button =>
                button.Content is string label && label is "Original" or "Reset" or "Save"
            )
            .ToArray();

    private static bool SaveReview(ImageAdjustmentPanel panel, Size size)
    {
        var folder = Environment.GetEnvironmentVariable("OPENPHYSICAL_REVIEW_IMAGES");
        if (string.IsNullOrWhiteSpace(folder))
            return false;
        var root = (Control)panel.GetVisualRoot()!;
        using var bitmap = new RenderTargetBitmap(
            new PixelSize(
                (int)Math.Ceiling(root.Bounds.Width),
                (int)Math.Ceiling(root.Bounds.Height)
            )
        );
        bitmap.Render(root);
        bitmap.Save(Path.Combine(folder, $"studio-{size.Width}x{size.Height}-adjustments.png"));
        return true;
    }
}
