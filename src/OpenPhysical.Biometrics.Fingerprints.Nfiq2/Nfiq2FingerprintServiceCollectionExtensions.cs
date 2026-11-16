// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenPhysical.Biometrics.NFIQ2;

/// <summary>Registers opt-in NFIQ 2 processing without changing NFIQ 1 or PIV registrations.</summary>
public static class Nfiq2FingerprintServiceCollectionExtensions
{
    /// <summary>Registers a reusable singleton native model and supplemental quality provider.</summary>
    /// <param name="services">The application's service registrations.</param>
    /// <returns>The same service collection for chained registration.</returns>
    /// <remarks>The container owns and disposes the native engine. Existing engine and provider registrations are preserved.</remarks>
    public static IServiceCollection AddNfiq2FingerprintQuality(this IServiceCollection services)
    {
        if (!services.Any(registration => registration.ServiceType == typeof(INfiq2Engine)))
            services.AddNfiq2();
        services.TryAddSingleton<
            IFingerprintUnifiedQualityProvider,
            Nfiq2FingerprintQualityProvider
        >();
        return services;
    }
}
