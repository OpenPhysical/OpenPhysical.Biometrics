// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Capture.Usb;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Creates the Studio host and its application services.</summary>
public static class DesktopServices
{
    /// <summary>Creates an unstarted host whose lifetime is owned by the caller.</summary>
    /// <returns>The configured desktop host.</returns>
    public static IHost CreateHost() => CreateHost(services => services);

    /// <summary>Creates a host with trusted feature registrations applied after the shared services.</summary>
    /// <param name="configure">Application-owned registrations and explicit service replacements.</param>
    /// <returns>The unstarted host whose lifetime belongs to the main window.</returns>
    public static IHost CreateHost(Func<IServiceCollection, IServiceCollection> configure)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory }
        );
        var services = builder.Services;
        services.AddSingleton(new DesktopApplicationIdentity("OpenPhysical Biometric Studio"));
        AddCapture(services);
        AddFacialWorkspace(services);
        AddStorage(services);
        services.AddSingleton<TemplateComparison>();
        services.AddSingleton<DesktopFilePicker>();
        services.AddSingleton<CaptureViewModel>();
        services.AddSingleton<ICapturedFingerprintSource>(provider =>
            provider.GetRequiredService<CaptureViewModel>()
        );
        AddWorkspace(services);
        services.AddSingleton<StudioPersonSelection>();
        services.AddSingleton<StudioFeatureCoordinator>();
        services.AddSingleton<
            IStudioVerificationBackendFactory,
            StudioVerificationRuntimeFactory
        >();
        services.AddSingleton<StudioCardVerificationViewModel>();
        services.AddSingleton<IStudioWorkspaceFeature, StudioVerificationFeature>();
        configure(services);
        return builder.Build();
    }

    private static IServiceCollection AddCapture(IServiceCollection services)
    {
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
        return services;
    }

    private static IServiceCollection AddFacialWorkspace(IServiceCollection services)
    {
        services.AddPortraitPreparation();
        services.AddFacialRecordPreparation();
        services.AddSingleton<ICameraProvider>(_ =>
            OperatingSystem.IsMacOS()
                ? new AvFoundationCameraProvider()
                : new FlashCapCameraProvider()
        );
        services.AddSingleton<PersonEnrollmentViewModel>();
        services.AddSingleton<FaceCaptureViewModel>();
        services.AddSingleton<PackageProtectionViewModel>();
        services.AddSingleton<DesktopPackageDialogs>();
        services.AddSingleton(
            new CameraQualificationStorage(CameraQualificationStorage.DefaultPath)
        );
        services.AddSingleton<CameraSetupViewModel>();
        services.AddSingleton<IEnrollmentPackageStorage, EnrollmentPackageStorage>();
        services.AddSingleton(provider => new BiometricPrivacySession(
            provider.GetRequiredService<PackageProtectionViewModel>(),
            () => provider.GetRequiredService<StudioFeatureCoordinator>().PauseAsync(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BiometricPrivacySession>>()
        ));
        return services;
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
            .AddSingleton(provider => new ExportViewModel(
                provider.GetRequiredService<CaptureViewModel>(),
                provider.GetRequiredService<IEnrollmentPackageStorage>(),
                provider.GetRequiredService<EnrollmentFingerSuggestions>(),
                provider.GetRequiredService<InspectionViewModel>(),
                provider.GetRequiredService<PersonEnrollmentViewModel>(),
                [new FaceEnrollmentSection(provider.GetRequiredService<FaceCaptureViewModel>())],
                provider.GetRequiredService<PackageProtectionViewModel>(),
                provider.GetRequiredService<DesktopFilePicker>(),
                provider.GetRequiredService<CameraSetupViewModel>()
            ))
            .AddSingleton<InspectionViewModel>()
            .AddSingleton(new SettingsStorage(SettingsStorage.DefaultPath))
            .AddSingleton(provider => new SettingsViewModel(
                provider.GetRequiredService<SettingsStorage>(),
                preferences =>
                    SettingsApplication.ApplyAsync(
                        preferences,
                        provider.GetRequiredService<CaptureViewModel>(),
                        provider.GetRequiredService<InspectionViewModel>(),
                        provider.GetRequiredService<FaceCaptureViewModel>()
                    ),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SettingsViewModel>>()
            ))
            .AddSingleton<WorkspaceViewModel>();

    private static IServiceCollection AddStorage(IServiceCollection services)
    {
        services.AddSingleton<TemplateStorage>();
        return services.AddSingleton<ITemplateStorage>(provider =>
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
