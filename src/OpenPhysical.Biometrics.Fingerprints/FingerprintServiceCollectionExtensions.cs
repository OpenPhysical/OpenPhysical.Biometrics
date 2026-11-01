// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NFIQ1;

/// <summary>Registers independently selectable fingerprint processing services.</summary>
public static class FingerprintServiceCollectionExtensions
{
    /// <summary>Registers NBIS extraction, comparison, and PIV enrollment services.</summary>
    /// <param name="services">The application's dependency-injection services.</param>
    /// <returns>The same collection for chained registration.</returns>
    /// <remarks>Register an <see cref="IFingerprintQualityProvider"/> for enrollment. Native processing
    /// initializes when an engine operation is first invoked.</remarks>
    public static IServiceCollection AddFingerprintProcessing(this IServiceCollection services)
    {
        services.TryAddSingleton<IFingerprintEngine, NbisFingerprintEngine>();
        services.TryAddTransient<FingerprintComparisonService>();
        services.TryAddTransient<PivFingerprintEnrollmentService>();
        return services;
    }

    /// <summary>Registers the NIST NFIQ 1 engine and its PIV enrollment quality adapter.</summary>
    /// <param name="services">The application's dependency-injection services.</param>
    /// <returns>The same collection for chained registration.</returns>
    public static IServiceCollection AddNfiq1FingerprintQuality(this IServiceCollection services)
    {
        services.TryAddSingleton<INfiq1Engine, Nfiq1Engine>();
        services.TryAddSingleton<IFingerprintQualityProvider, Nfiq1FingerprintQualityProvider>();
        return services;
    }
}
