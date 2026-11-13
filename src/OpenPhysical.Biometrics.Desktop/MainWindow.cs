// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using Avalonia.Data;
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
    private readonly StudioFeatureCoordinator features;
    private bool closed;
    private bool closing;
    private bool initialized;
    private readonly BiometricPrivacySession privacy;
    private readonly DesktopPackageDialogs dialogs;
    private readonly DesktopPackageReviewPresenter packageReview;
    private readonly DesktopSettingsNotifications settingsNotifications;

    /// <summary>Creates a window with application services owned by this window.</summary>
    public MainWindow()
        : this(DesktopServices.CreateHost()) { }

    /// <summary>Creates a shared Studio window with an application-owned host.</summary>
    /// <param name="host">Trusted services owned and disposed by this window.</param>
    public MainWindow(IHost host)
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
        features = host.Services.GetRequiredService<StudioFeatureCoordinator>();
        AvaloniaXamlLoader.Load(this);
        AddFeatures();
        DataContext = workspace;
        picker.Attach(this);
        dialogs = host.Services.GetRequiredService<DesktopPackageDialogs>();
        dialogs.Attach(this, workspace.Export.Protection);
        packageReview = DesktopPackageReviewPresenter.Attach(this, workspace.Export);
        settingsNotifications = DesktopSettingsNotifications.Attach(
            this,
            workspace.Settings,
            host.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DesktopSettingsNotifications>>()
        );
        workspace.Export.Protection.AttachLockAction(() => features.PauseAsync());
        privacy = host.Services.GetRequiredService<BiometricPrivacySession>();
        Opened += async (_, _) => await StartAsync();
        Closing += (_, args) => HandleClosing(args);
    }

    private CSharpFunctionalExtensions.UnitResult<DesktopError> AddFeatures()
    {
        Title = host.Services.GetRequiredService<DesktopApplicationIdentity>().Name;
        if (this.FindControl<TabControl>("WorkspaceTabs") is { } tabs)
            foreach (var feature in features.Features)
            {
                var tab = new TabItem { Header = feature.Title, Content = feature.Content };
                tab.Bind(IsEnabledProperty, new Binding("!Export.Enrollment.IsEditing"));
                tabs.Items.Add(tab);
            }
        if (this.FindControl<TabControl>("WorkspaceTabs") is { } navigation)
            navigation.SelectedIndex = host
                .Services.GetRequiredService<DesktopApplicationIdentity>()
                .InitialWorkspaceIndex;
        if (this.FindControl<Button>("ClearPersonButton") is { } clear)
            clear.Command = features.ClearPersonCommand;
        return CSharpFunctionalExtensions.UnitResult.Success<DesktopError>();
    }

    internal CSharpFunctionalExtensions.UnitResult<DesktopError> ShowCameraSetup()
    {
        if (
            this.FindControl<Button>("SettingsButton") is not { Flyout: { } flyout } button
            || this.FindControl<Expander>("CameraSetupExpander") is not { } setup
        )
            return new DesktopError("Camera.SetupPresenter", "Open Camera setup in Settings.");
        setup.IsExpanded = true;
        flyout.ShowAt(button);
        return CSharpFunctionalExtensions.UnitResult.Success<DesktopError>();
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
                await workspace.Settings.InitializeAsync();
                var setup = await host
                    .Services.GetRequiredService<CameraSetupViewModel>()
                    .InitializeAsync();
                if (setup.IsFailure)
                    workspace.Export.Report(setup.Error);
                var monitoring = privacy.StartMonitoring();
                if (monitoring.IsFailure)
                    return monitoring;
                initialized = true;
                return await features.SelectAsync(
                    this.FindControl<TabControl>("WorkspaceTabs") is { } tabs
                        ? tabs.SelectedIndex
                        : -1
                );
            },
            "Startup",
            "Cannot start capture services."
        );
        if (started.IsFailure)
            workspace.Export.Report(started.Error);
        else if (started.Value.IsFailure)
            workspace.Export.Report(started.Value.Error);
    }

    private async void CaptureTabChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!initialized || sender is not TabControl tabs || !ReferenceEquals(args.Source, tabs))
            return;
        var activated = await features.SelectAsync(tabs.SelectedIndex);
        if (activated.IsFailure && !closing)
            workspace.Export.Report(activated.Error);
    }

    private async void HandleClosing(WindowClosingEventArgs args)
    {
        if (closed)
            return;
        args.Cancel = true;
        if (closing)
            return;
        closing = true;
        initialized = false;
        await DesktopEdge.TryAsync(
            async () =>
            {
                dialogs.Dispose();
                packageReview.Dispose();
                settingsNotifications.Dispose();
                privacy.Dispose();
                await privacy.DrainAsync();
                await features.StopAsync();
                await workspace.Export.StopAsync();
                await Task.WhenAll(
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
