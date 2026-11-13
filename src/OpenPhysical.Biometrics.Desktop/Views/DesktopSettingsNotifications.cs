// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

/// <summary>Shows brief Material notifications within the owning window, without interrupting capture.</summary>
public sealed class DesktopSettingsNotifications : IDisposable
{
    private readonly Window owner;
    private readonly SettingsViewModel settings;
    private readonly ILogger<DesktopSettingsNotifications> logger;
    private readonly WindowNotificationManager manager;
    private bool disposed;

    private DesktopSettingsNotifications(
        Window owner,
        SettingsViewModel settings,
        ILogger<DesktopSettingsNotifications> logger
    )
    {
        this.owner = owner;
        this.settings = settings;
        this.logger = logger;
        manager = new WindowNotificationManager(owner)
        {
            Position = NotificationPosition.TopRight,
            MaxItems = 1,
        };
        settings.PropertyChanged += Changed;
        owner.Opened += Opened;
        owner.Closed += Closed;
        Refresh();
    }

    /// <summary>Attaches one transient notice presenter, including notices queued before the window opens.</summary>
    /// <param name="owner">Shown Studio window.</param>
    /// <param name="settings">Settings form and pending notices.</param>
    /// <param name="logger">Detailed presentation diagnostics.</param>
    /// <returns>The subscription owned by the window lifetime.</returns>
    public static DesktopSettingsNotifications Attach(
        Window owner,
        SettingsViewModel settings,
        ILogger<DesktopSettingsNotifications> logger
    ) => new(owner, settings, logger);

    private void Changed([AllowNull] object sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(SettingsViewModel.Notice))
            return;
        if (Dispatcher.UIThread.CheckAccess())
            Refresh();
        else
            Dispatcher.UIThread.Post(() => Refresh());
    }

    private UnitResult<DesktopError> Refresh()
    {
        if (disposed || !owner.IsVisible || settings.Notice.HasNoValue)
            return UnitResult.Success<DesktopError>();
        var notice = settings.Notice.Value;
        var shown = DesktopEdge.Try(
            () =>
            {
                manager.Show(
                    new Notification(
                        "Settings",
                        notice.Message,
                        notice.IsError ? NotificationType.Error : NotificationType.Warning,
                        TimeSpan.FromSeconds(6)
                    )
                );
                return true;
            },
            "Settings.Notice",
            "The settings notice could not be shown."
        );
        if (shown.IsFailure)
            logger.LogWarning(
                "Settings notice failed ({Code}): {Message}",
                shown.Error.Code,
                shown.Error.Message
            );
        settings.DismissNotice();
        return shown.IsFailure
            ? UnitResult.Failure(shown.Error)
            : UnitResult.Success<DesktopError>();
    }

    private void Opened([AllowNull] object sender, EventArgs args) => Refresh();

    private void Closed([AllowNull] object sender, EventArgs args) => Dispose();

    /// <summary>Detaches notifications before the window and services shut down.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        settings.PropertyChanged -= Changed;
        owner.Opened -= Opened;
        owner.Closed -= Closed;
        manager.CloseAll();
    }
}
