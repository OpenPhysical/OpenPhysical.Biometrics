// SPDX-License-Identifier: GPL-2.0-or-later
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Registers actual comparison only in the capture host.</summary>
public static class CaptureVerificationRegistration
{
    /// <summary>The host selects and configures its fingerprint engine and capture drivers separately.</summary>
    /// <param name="services">Host service registrations.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public static IServiceCollection AddCredentialFingerprintVerification(
        this IServiceCollection services
    )
    {
        services.AddCredentialVerification();
        services.TryAddSingleton<FingerprintComparisonService>();
        services.TryAddSingleton<FingerprintCeremonyService>();
        return services;
    }
}
