// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Media;

namespace OpenPhysical.Biometrics.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    internal static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<DesktopApplication>()
            .UsePlatformDetect()
            .With(
                new FontManagerOptions
                {
                    DefaultFamilyName = "avares://Material.Styles/Fonts/Roboto#Roboto",
                }
            );
}
