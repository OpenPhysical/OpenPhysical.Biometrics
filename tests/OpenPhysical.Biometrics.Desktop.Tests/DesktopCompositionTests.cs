// SPDX-License-Identifier: GPL-2.0-or-later
using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

[assembly: AvaloniaTestApplication(
    typeof(OpenPhysical.Biometrics.Desktop.Tests.DesktopTestApplication)
)]

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal static class DesktopTestApplication
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<DesktopApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

[TestFixture]
internal sealed class DesktopCompositionTests
{
    [AvaloniaTest]
    public async Task HostResolvesWorkspaceAndCompiledWindowWithoutOpeningHardware()
    {
        var host = DesktopServices.CreateHost();
        var workspace = host.Services.GetRequiredService<WorkspaceViewModel>();
        var picker = host.Services.GetRequiredService<DesktopFilePicker>();
        var window = new MainWindow(host, workspace, picker);
        window.Title.Should().Be("OpenPhysical Biometric Studio");
        Application
            .Current.Should()
            .BeOfType<DesktopApplication>()
            .Which.Name.Should()
            .Be("OpenPhysical Biometric Studio");
        var assembly = typeof(DesktopApplication).Assembly;
        assembly
            .GetCustomAttribute<AssemblyTitleAttribute>()
            .Should()
            .BeOfType<AssemblyTitleAttribute>()
            .Which.Title.Should()
            .Be("OpenPhysical Biometric Studio");
        assembly
            .GetCustomAttribute<AssemblyProductAttribute>()
            .Should()
            .BeOfType<AssemblyProductAttribute>()
            .Which.Product.Should()
            .Be("OpenPhysical Biometric Studio");
        window.DataContext.Should().BeSameAs(workspace);
        workspace.Capture.CaptureCommand.CanExecute(default).Should().BeFalse();
        workspace.Inspection.Threshold.Should().Be(40);
        await ((IAsyncDisposable)host).DisposeAsync();
    }
}
