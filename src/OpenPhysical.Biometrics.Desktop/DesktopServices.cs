// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Capture.Usb;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Creates the Studio host and its application services.</summary>
public static class DesktopServices
{
    /// <summary>Creates an unstarted host whose lifetime is owned by the caller.</summary>
    /// <returns>The configured desktop host.</returns>
    public static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory }
        );
        var services = builder.Services;
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IUsbDeviceProvider>(_ => BundledUsbProvider());
        services.AddFs50Capture();
        services.AddSingleton(provider =>
            CaptureDriverRegistry
                .Create(
                    [.. provider.GetServices<IFingerprintCaptureDriver>()],
                    provider.GetRequiredService<TimeProvider>()
                )
                .Value
        );
        AddProcessing(services);
        services.AddSingleton<DesktopPreviewDispatcher>();
        services.AddSingleton<IPreviewDispatcher>(provider =>
            provider.GetRequiredService<DesktopPreviewDispatcher>()
        );
        services.AddSingleton(provider => new CaptureCoordinator(
            provider.GetRequiredService<CaptureDriverRegistry>(),
            provider.GetRequiredService<IPreviewDispatcher>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CaptureCoordinator>>()
        ));
        AddStorage(services);
        services.AddSingleton<TemplateComparison>();
        services.AddSingleton<DesktopFilePicker>();
        services.AddSingleton<CaptureViewModel>();
        services.AddSingleton<ICapturedFingerprintSource>(provider =>
            provider.GetRequiredService<CaptureViewModel>()
        );
        AddWorkspace(services);
        return builder.Build();
    }

    private static IServiceCollection AddProcessing(IServiceCollection services) =>
        services
            .AddFingerprintProcessing()
            .AddNfiq1FingerprintQuality()
            .AddNfiq2FingerprintQuality()
            .AddSingleton<ICaptureProcessor, CaptureProcessor>();

    private static IServiceCollection AddWorkspace(IServiceCollection services) =>
        services
            .AddSingleton<ICredentialTransport, PcscCredentialTransport>()
            .AddSingleton<CredentialReaderService>()
            .AddSingleton<EnrollmentFingerSuggestions>()
            .AddSingleton<ExportViewModel>()
            .AddSingleton<InspectionViewModel>()
            .AddSingleton(new SettingsStorage(SettingsStorage.DefaultPath))
            .AddSingleton(provider => new SettingsViewModel(
                provider.GetRequiredService<SettingsStorage>(),
                provider.GetRequiredService<CaptureViewModel>(),
                provider.GetRequiredService<InspectionViewModel>()
            ))
            .AddSingleton<WorkspaceViewModel>();

    private static IServiceCollection AddStorage(IServiceCollection services)
    {
        services.AddSingleton<TemplateStorage>();
        return services
            .AddSingleton<ITemplateStorage>(provider =>
                provider.GetRequiredService<TemplateStorage>()
            )
            .AddSingleton<ITemplateSessionStorage>(provider =>
                provider.GetRequiredService<TemplateStorage>()
            );
    }

    private static IUsbDeviceProvider BundledUsbProvider() =>
        LibUsbDeviceProvider
            .LocateBundledLibrary()
            .Match(
                path => (IUsbDeviceProvider)new LibUsbDeviceProvider(path),
                error => new UnavailableUsbDeviceProvider(error)
            );
}
