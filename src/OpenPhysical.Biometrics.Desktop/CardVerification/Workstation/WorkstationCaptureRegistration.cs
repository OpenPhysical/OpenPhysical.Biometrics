// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Capture.Usb;
using OpenPhysical.Biometrics.Desktop;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Workstation.Application.Verification;
using OpenPhysical.Workstation.Infrastructure.Verification;

namespace OpenPhysical.Workstation.Infrastructure;

/// <summary>Optional capture hardware for the verification workstation, excluded from the signing application.</summary>
public static class WorkstationCaptureRegistration
{
    /// <summary>Add workstation capture.</summary>
    /// <param name="services">Host service registrations.</param>
    /// <param name="libUsbPath">Explicit absolute USB library path when USB capture is configured.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public static Result<IServiceCollection, DesktopError> AddWorkstationCapture(
        this IServiceCollection services,
        Maybe<string> libUsbPath = default
    )
    {
        if (
            libUsbPath.HasValue
            && (
                !Path.IsPathFullyQualified(libUsbPath.Value)
                || string.IsNullOrWhiteSpace(libUsbPath.Value)
            )
        )
            return new DesktopError(
                "Verification.UsbPath",
                "Choose an absolute capture library path."
            );
        AddVerificationServices(services);
        if (libUsbPath.HasValue)
        {
            services.AddSingleton<IUsbDeviceProvider>(new LibUsbDeviceProvider(libUsbPath.Value));
            services.AddFs50Capture();
        }
        services.TryAddSingleton(provider =>
            CaptureDriverRegistry
                .Create(
                    provider.GetServices<IFingerprintCaptureDriver>().ToImmutableArray(),
                    provider.GetRequiredService<TimeProvider>()
                )
                .Value
        );
        services.AddSingleton<WorkstationCaptureDevices>();
        services.AddSingleton<IGuidedVerificationRunner, GuidedVerificationRunner>();
        services.AddSingleton<GuidedVerificationCoordinator>();
        return Result.Success<IServiceCollection, DesktopError>(services);
    }

    private static IServiceCollection AddVerificationServices(IServiceCollection services)
    {
        services.AddFingerprintProcessing();
        services.AddSingleton<FingerprintCeremonyService>();
        services.AddSingleton<LiveFingerprintVerificationService>();
        services.AddSingleton<TwicAcceptanceService>();
        services.AddSingleton(provider => new LiveTwicFingerprintVerificationService(
            provider.GetRequiredService<CredentialCeremonyService>(),
            provider.GetRequiredService<TwicAcceptanceService>(),
            provider.GetRequiredService<FingerprintCeremonyService>(),
            provider.GetRequiredService<CaptureDriverRegistry>(),
            provider.GetRequiredService<AcquiredFingerprintAdapter>(),
            new OpenPhysical.Middleware.Infrastructure.PivApduExecutor(),
            provider.GetRequiredService<TimeProvider>()
        ));
        return services;
    }
}
