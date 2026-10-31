namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;

/// <summary>Registers application-owned signing and trust services for the compatibility CLI.</summary>
public static class CryptographyServiceExtensions
{
    /// <summary>Adds the CLI's CMS implementation and certificate discovery policy.</summary>
    /// <param name="services">The application's dependency container.</param>
    /// <returns>The same container for further application configuration.</returns>
    public static IServiceCollection AddCompatibilityCryptographyServices(
        this IServiceCollection services
    )
    {
        services.AddScoped<ICbeffSigner, CbeffSignerService>();
        services.AddScoped<ICbeffVerifier, CbeffVerifierService>();
        services.AddScoped<ICertificateDiscoveryService, CertificateDiscoveryService>();
        services.AddScoped<IPivCrossValidationService, PivCrossValidationService>();
        return services;
    }
}
