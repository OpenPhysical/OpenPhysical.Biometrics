// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Studio window owning the application service lifetime.</summary>
public sealed class MainWindow : Window
{
    private readonly IHost host;
    private readonly WorkspaceViewModel workspace;
    private bool closed;
    private bool closing;

    /// <summary>Creates a window with application services owned by this window.</summary>
    public MainWindow()
        : this(DesktopServices.CreateHost()) { }

    private MainWindow(IHost host)
        : this(
            host,
            host.Services.GetRequiredService<WorkspaceViewModel>(),
            host.Services.GetRequiredService<DesktopFilePicker>()
        ) { }

    /// <summary>Creates a workspace window and takes ownership of its host.</summary>
    /// <param name="host">Services started on opening and disposed on closing.</param>
    /// <param name="workspace">Operator workspace state.</param>
    /// <param name="picker">File dialogs attached to this window.</param>
    public MainWindow(IHost host, WorkspaceViewModel workspace, DesktopFilePicker picker)
    {
        this.host = host;
        this.workspace = workspace;
        AvaloniaXamlLoader.Load(this);
        DataContext = workspace;
        picker.Attach(this);
        Opened += async (_, _) => await StartAsync();
        Closing += (_, args) => HandleClosing(args);
    }

    private void CloseSettings(object sender, RoutedEventArgs args)
    {
        if (this.FindControl<Button>("SettingsButton") is { Flyout: { } flyout })
            flyout.Hide();
    }

    private async Task StartAsync()
    {
        var started = await DesktopEdge.TryAsync(
            async () =>
            {
                await host.StartAsync();
                var settings = await workspace.Settings.InitializeAsync();
                if (settings.IsFailure)
                    workspace.Capture.Report(settings.Error);
                return await workspace.Capture.StartAsync(CancellationToken.None);
            },
            "Startup",
            "Cannot start capture services."
        );
        if (started.IsFailure)
            workspace.Capture.Report(started.Error);
        else if (started.Value.IsFailure)
            workspace.Capture.Report(started.Value.Error);
    }

    private async void HandleClosing(WindowClosingEventArgs args)
    {
        if (closed)
            return;
        args.Cancel = true;
        if (closing)
            return;
        closing = true;
        await DesktopEdge.TryAsync(
            async () =>
            {
                await Task.WhenAll(
                    workspace.Capture.StopAsync(),
                    workspace.Export.StopAsync(),
                    workspace.Inspection.StopAsync(),
                    workspace.Settings.StopAsync()
                );
                await host.StopAsync();
                if (host is IAsyncDisposable asynchronous)
                    await asynchronous.DisposeAsync();
                else
                    host.Dispose();
                return true;
            },
            "Shutdown",
            "Cannot stop capture services."
        );
        closed = true;
        Close();
    }
}
