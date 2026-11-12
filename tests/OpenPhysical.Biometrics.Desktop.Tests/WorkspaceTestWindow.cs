// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal static class WorkspaceTestWindow
{
    internal static Window Create(IHost host, WorkspaceViewModel workspace, Size size)
    {
        var main = new MainWindow(
            host,
            workspace,
            host.Services.GetRequiredService<DesktopFilePicker>()
        );
        var content = (Control)main.Content!;
        main.Content = default;
        return new Window
        {
            DataContext = workspace,
            Content = content,
            Width = size.Width,
            Height = size.Height,
        };
    }
}
