// SPDX-License-Identifier: GPL-2.0-or-later
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
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
            .Configure<SharedShellTestApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

internal sealed class SharedShellTestApplication : DesktopApplication { }

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
            .BeAssignableTo<DesktopApplication>()
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

    [AvaloniaTest]
    public async Task TrustedFeatureUsesTheCompiledSharedShellAndApplicationTitle()
    {
        var feature = new StudioTestFeature();
        var host = DesktopServices.CreateHost(services =>
            services
                .AddSingleton(new DesktopApplicationIdentity("Studio feature review", 3))
                .AddSingleton<IStudioWorkspaceFeature>(feature)
        );
        var window = new MainWindow(host);
        window.Title.Should().Be("Studio feature review");
        var tabs = window.FindControl<Avalonia.Controls.TabControl>("WorkspaceTabs");
        tabs.Should().NotBeNull();
        tabs!.Items.Should().HaveCount(4);
        tabs.SelectedIndex.Should().Be(3);
        tabs.Items.OfType<TabItem>().ElementAt(2).Header.Should().Be("Check card");
        var task = tabs.Items.OfType<Avalonia.Controls.TabItem>().Last();
        task.Header.Should().Be(feature.Title);
        task.Content.Should().BeSameAs(feature.Content);
        Application.Current!.Styles.Should().NotBeEmpty();
        await ((IAsyncDisposable)host).DisposeAsync();
    }
}
