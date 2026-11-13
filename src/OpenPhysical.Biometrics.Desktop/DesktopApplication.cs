// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Avalonia entry point for OpenPhysical Biometric Studio.</summary>
public class DesktopApplication : StudioPresentationApplication
{
    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var host = CreateHost();
            Name = host.Services.GetRequiredService<DesktopApplicationIdentity>().Name;
            desktop.MainWindow = new MainWindow(host);
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Creates trusted application services sharing the Studio shell and source lifetime.</summary>
    /// <returns>An unstarted host owned by the main window.</returns>
    protected virtual IHost CreateHost() => DesktopServices.CreateHost();
}
