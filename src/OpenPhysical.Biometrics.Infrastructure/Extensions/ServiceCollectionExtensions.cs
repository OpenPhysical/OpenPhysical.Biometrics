namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Biometrics.Infrastructure.Certificates;

/// <summary>
/// Extension methods for registering infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers production certificate store services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCertificateStore(this IServiceCollection services)
    {
        services.AddOptions<TrustedCertificateStoreOptions>();
        services.AddSingleton<ITrustedCertificateProvider, TrustedCertificateProvider>();
        services.AddSingleton<ITrustedCertificateStore, TrustedCertificateStore>();
        
        // Register Lazy<ITrustedCertificateProvider> for the store dependency
        services.AddSingleton<Lazy<ITrustedCertificateProvider>>(provider =>
            new Lazy<ITrustedCertificateProvider>(() => provider.GetRequiredService<ITrustedCertificateProvider>()));
        
        return services;
    }

    /// <summary>
    /// Registers certificate store services with configuration from IConfiguration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="sectionName">The configuration section name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCertificateStore(
        this IServiceCollection services, 
        IConfiguration configuration,
        string sectionName = "CertificateStore")
    {
        services.Configure<TrustedCertificateStoreOptions>(configuration.GetSection(sectionName));
        services.AddSingleton<ITrustedCertificateProvider, TrustedCertificateProvider>();
        services.AddSingleton<ITrustedCertificateStore, TrustedCertificateStore>();
        
        // Register Lazy<ITrustedCertificateProvider> for the store dependency
        services.AddSingleton<Lazy<ITrustedCertificateProvider>>(provider =>
            new Lazy<ITrustedCertificateProvider>(() => provider.GetRequiredService<ITrustedCertificateProvider>()));
        
        return services;
    }

    /// <summary>
    /// Registers certificate store services with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCertificateStore(
        this IServiceCollection services,
        Action<TrustedCertificateStoreOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<ITrustedCertificateProvider, TrustedCertificateProvider>();
        services.AddSingleton<ITrustedCertificateStore, TrustedCertificateStore>();
        
        // Register Lazy<ITrustedCertificateProvider> for the store dependency
        services.AddSingleton<Lazy<ITrustedCertificateProvider>>(provider =>
            new Lazy<ITrustedCertificateProvider>(() => provider.GetRequiredService<ITrustedCertificateProvider>()));
        
        return services;
    }

    /// <summary>
    /// Registers in-memory certificate store for testing with test certificates.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestCertificateStore(this IServiceCollection services)
    {
        // Use a simple provider that returns the static test certificates
        var provider = new CertificateStoreBuilder()
            .WithAllTestCertificates()
            .Build();
        
        services.AddSingleton<ITrustedCertificateStore>(provider);
        
        return services;
    }

    /// <summary>
    /// Registers in-memory certificate store with custom builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="builderAction">Action to configure the certificate store builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestCertificateStore(
        this IServiceCollection services,
        Action<CertificateStoreBuilder> builderAction)
    {
        var builder = new CertificateStoreBuilder();
        builderAction(builder);
        var store = builder.Build();
        
        services.AddSingleton<ITrustedCertificateStore>(store);
        
        return services;
    }
}