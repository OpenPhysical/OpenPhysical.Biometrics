// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Avalonia entry point for OpenPhysical Biometric Studio.</summary>
public sealed class DesktopApplication : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        Name = "OpenPhysical Biometric Studio";
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
