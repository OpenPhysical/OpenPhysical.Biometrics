// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

[TestFixture]
internal sealed class DesktopSettingsNotificationsTests
{
    [AvaloniaTest]
    public async Task RecoveryQueuedBeforeOpeningShowsOneNonblockingMaterialNotice()
    {
        using var location = new SettingsTestFile();
        Directory.CreateDirectory(location.Folder);
        await File.WriteAllTextAsync(location.Path, "{");
        var settings = new SettingsViewModel(
            new SettingsStorage(location.Path),
            _ => Task.FromResult(UnitResult.Success<DesktopError>()),
            NullLogger<SettingsViewModel>.Instance
        );
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new TextBlock { Text = "Capture workspace" },
        };
        using var presenter = DesktopSettingsNotifications.Attach(
            window,
            settings,
            NullLogger<DesktopSettingsNotifications>.Instance
        );
        await settings.InitializeAsync();
        window.IsVisible.Should().BeFalse();
        settings.Notice.HasValue.Should().BeTrue();
        window.Show();
        var card = await DisplayedNoticeAsync(window);
        AssertVisibleNotice(card, window);
        settings.Notice.HasNoValue.Should().BeTrue();
        window.OwnedWindows.Should().BeEmpty();
        WorkspaceReviewImages.Save(window, new Size(800, 600), "settings-reset");
        window.Close();
    }

    private static bool AssertVisibleNotice(NotificationCard card, Window window)
    {
        var position = card.TranslatePoint(default, window)!.Value;
        position.Y.Should().BeGreaterThanOrEqualTo(0);
        position.X.Should().BeGreaterThanOrEqualTo(0);
        (position.X + card.Bounds.Width).Should().BeLessThanOrEqualTo(window.Bounds.Width);
        (position.Y + card.Bounds.Height).Should().BeLessThanOrEqualTo(window.Bounds.Height);
        ((INotification)card.Content!).Message.Should().Be("Unable to load settings, resetting.");
        card.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
        foreach (var text in card.GetVisualDescendants().OfType<TextBlock>())
        {
            ((ISolidColorBrush)text.Foreground!).Color.Should().Be(Colors.White);
            text.FontSize.Should().Be(16);
        }
        return true;
    }

    private static async Task<NotificationCard> DisplayedNoticeAsync(Window window)
    {
        for (var frame = 0; frame < 30; frame++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        window.UpdateLayout();
        return window
            .GetVisualDescendants()
            .OfType<NotificationCard>()
            .Should()
            .ContainSingle()
            .Which;
    }
}
