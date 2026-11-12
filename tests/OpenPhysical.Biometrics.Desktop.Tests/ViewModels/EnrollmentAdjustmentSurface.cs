// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal sealed class EnrollmentAdjustmentSurface : IAsyncDisposable
{
    private readonly IHost host = DesktopServices.CreateHost();
    private readonly Window window;
    private readonly Size size;
    private readonly string title;

    internal EnrollmentAdjustmentSurface(EnrollmentAdjustmentContext rig, Size size)
    {
        this.size = size;
        title = rig.Editor.Title;
        var workspace = new WorkspaceViewModel(
            rig.Capture,
            rig.Export,
            rig.Inspection,
            host.Services.GetRequiredService<SettingsViewModel>()
        );
        window = WorkspaceTestWindow.Create(host, workspace, size);
        window.Show();
    }

    internal bool AssertEditorFits()
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var controls = window.GetVisualDescendants().OfType<Control>().ToArray();
        AssertActions(controls.OfType<Button>());
        var text = controls
            .OfType<TextBlock>()
            .Where(value => value.IsEffectivelyVisible)
            .ToArray();
        AssertVisible(text.Single(value => value.Text == title));
        AssertVisible(text.Single(value => value.Text?.Contains("Contrast 0 → 30") == true));
        controls
            .OfType<Button>()
            .Should()
            .NotContain(value =>
                value.IsEffectivelyVisible
                && (
                    Equals(value.Content, "Accept finger")
                    || Equals(value.Content, "Save person")
                    || Equals(value.Content, "Reset capture")
                )
            );
        WorkspaceReviewImages.Save(window, size, "editing-capture");
        foreach (var image in controls.OfType<BiometricImageView>())
            image.Invert = true;
        WorkspaceReviewImages.Save(window, size, "editing-capture-invert");
        return true;
    }

    internal bool AssertReviewActions(bool pending)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var buttons = window.GetVisualDescendants().OfType<Button>().ToArray();
        var adjust = buttons.Single(value => Equals(value.Content, "Adjust capture"));
        adjust.IsEffectivelyVisible.Should().Be(pending);
        adjust.Command!.CanExecute(adjust.CommandParameter).Should().Be(pending);
        if (pending)
        {
            adjust.IsEffectivelyEnabled.Should().BeTrue();
            AssertVisible(adjust);
            var accept = buttons.Single(value => Equals(value.Content, "Accept finger"));
            AssertVisible(accept);
        }
        return true;
    }

    private bool AssertActions(IEnumerable<Button> buttons)
    {
        var actions = buttons
            .Where(value =>
                value.IsEffectivelyVisible && value.Content is "Apply changes" or "Cancel"
            )
            .ToArray();
        actions.Should().HaveCount(2);
        var apply = actions.Single(value => Equals(value.Content, "Apply changes"));
        apply.IsEnabled.Should().BeTrue();
        apply.IsEffectivelyEnabled.Should().BeTrue();
        apply.Command!.CanExecute(apply.CommandParameter).Should().BeTrue();
        foreach (var action in actions)
        {
            action.Bounds.Width.Should().BeGreaterThanOrEqualTo(48);
            action.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
            AssertVisible(action);
        }
        return true;
    }

    private bool AssertVisible(Control control)
    {
        control.IsEffectivelyVisible.Should().BeTrue();
        var bounds = new Rect(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);
        new Rect(window.Bounds.Size).Contains(bounds).Should().BeTrue();
        foreach (var scroll in control.GetVisualAncestors().OfType<ScrollViewer>())
        {
            var clip = new Rect(scroll.TranslatePoint(default, window)!.Value, scroll.Bounds.Size);
            clip.Contains(bounds).Should().BeTrue();
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        window.Close();
        await ((IAsyncDisposable)host).DisposeAsync();
    }
}
