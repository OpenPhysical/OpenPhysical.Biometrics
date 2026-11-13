// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Microsoft.Extensions.Hosting;
using OpenPhysical.Biometrics.Desktop;

namespace OpenPhysical.Biometrics.Studio;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "verify-card")
        {
            var builder = Host.CreateApplicationBuilder();
            return StudioVerificationApplication
                .RunHostedAsync(args.Skip(1).ToArray(), builder, Console.Out)
                .GetAwaiter()
                .GetResult();
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

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
