namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        services.AddSingleton<ITrustedCertificateStoreExtended>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<MultiIndexCertificateStore>>();
            var provider = serviceProvider.GetRequiredService<ITrustedCertificateProvider>();
            var lazyProvider = new Lazy<ITrustedCertificateProvider>(() => provider);

            var store = new MultiIndexCertificateStore(logger, lazyProvider);
            var initResult = store.CreateWithDefaults();

            if (initResult.IsSuccess)
            {
                return initResult.Value;
            }
            else
            {
                logger.LogError(
                    new EventId(879097, "ServiceCollectionExtensions"),
                    "ServiceCollectionExtensions failed."
                );
                return store;
            }
        });

        // Register base interface for signature verification
        services.AddSingleton<ITrustedCertificateStore>(serviceProvider =>
            serviceProvider.GetRequiredService<ITrustedCertificateStoreExtended>()
        );

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
        string sectionName = "CertificateStore"
    )
    {
        services.Configure<TrustedCertificateStoreOptions>(configuration.GetSection(sectionName));
        services.AddSingleton<ITrustedCertificateProvider, TrustedCertificateProvider>();

        services.AddSingleton<ITrustedCertificateStoreExtended>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<MultiIndexCertificateStore>>();
            var provider = serviceProvider.GetRequiredService<ITrustedCertificateProvider>();
            var lazyProvider = new Lazy<ITrustedCertificateProvider>(() => provider);

            var store = new MultiIndexCertificateStore(logger, lazyProvider);
            var initResult = store.CreateWithDefaults();

            if (initResult.IsSuccess)
            {
                return initResult.Value;
            }
            else
            {
                logger.LogError(
                    new EventId(668079, "ServiceCollectionExtensions"),
                    "ServiceCollectionExtensions failed."
                );
                return store;
            }
        });

        // Register base interface for signature verification
        services.AddSingleton<ITrustedCertificateStore>(serviceProvider =>
            serviceProvider.GetRequiredService<ITrustedCertificateStoreExtended>()
        );

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
        Action<TrustedCertificateStoreOptions> configureOptions
    )
    {
        services.Configure(configureOptions);
        services.AddSingleton<ITrustedCertificateProvider, TrustedCertificateProvider>();

        services.AddSingleton<ITrustedCertificateStoreExtended>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<MultiIndexCertificateStore>>();
            var provider = serviceProvider.GetRequiredService<ITrustedCertificateProvider>();
            var lazyProvider = new Lazy<ITrustedCertificateProvider>(() => provider);

            var store = new MultiIndexCertificateStore(logger, lazyProvider);
            var initResult = store.CreateWithDefaults();

            if (initResult.IsSuccess)
            {
                return initResult.Value;
            }
            else
            {
                logger.LogError(
                    new EventId(884533, "ServiceCollectionExtensions"),
                    "ServiceCollectionExtensions failed."
                );
                return store;
            }
        });

        // Register base interface for signature verification
        services.AddSingleton<ITrustedCertificateStore>(serviceProvider =>
            serviceProvider.GetRequiredService<ITrustedCertificateStoreExtended>()
        );

        return services;
    }

    /// <summary>
    /// Registers in-memory certificate store for testing with test certificates.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestCertificateStore(this IServiceCollection services)
    {
        // Use ICAM certificates from embedded resources for realistic testing
        services.AddSingleton<ITrustedCertificateProvider, TrustedCertificateProvider>();

        // Register the multi-index store for better certificate management
        services.AddSingleton<ITrustedCertificateStoreExtended>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<MultiIndexCertificateStore>>();
            var provider = serviceProvider.GetRequiredService<ITrustedCertificateProvider>();
            var lazyProvider = new Lazy<ITrustedCertificateProvider>(() => provider);

            var store = new MultiIndexCertificateStore(logger, lazyProvider);
            var initResult = store.CreateWithDefaults();

            if (initResult.IsSuccess)
            {
                return initResult.Value;
            }
            else
            {
                // Log error and return empty store
                logger.LogError(
                    new EventId(847043, "ServiceCollectionExtensions"),
                    "ServiceCollectionExtensions failed."
                );
                return store;
            }
        });

        // Register base interface for signature verification
        services.AddSingleton<ITrustedCertificateStore>(serviceProvider =>
            serviceProvider.GetRequiredService<ITrustedCertificateStoreExtended>()
        );

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
        Action<CertificateStoreBuilder> builderAction
    )
    {
        var builder = new CertificateStoreBuilder();
        builderAction(builder);
        var store = builder.Build();

        services.AddSingleton<ITrustedCertificateStore>(store);

        return services;
    }

    /// <summary>
    /// Registers certificate store services automatically based on the hosting environment.
    /// Uses test certificates in Development/Testing environments and production certificates otherwise.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="configuration">Optional configuration for production certificate store.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCertificateStoreForEnvironment(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration? configuration = null
    )
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            // Use test certificate store for development and testing
            return services.AddTestCertificateStore();
        }
        else
        {
            // Use production certificate store for staging and production
            if (configuration != null)
            {
                return services.AddCertificateStore(configuration);
            }
            else
            {
                return services.AddCertificateStore();
            }
        }
    }

    /// <summary>
    /// Registers certificate store services automatically based on the hosting environment with custom configuration.
    /// Uses test certificates in Development/Testing environments and production certificates with custom options otherwise.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="configureProductionOptions">Action to configure production certificate store options.</param>
    /// <param name="configureTestBuilder">Optional action to configure test certificate builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCertificateStoreForEnvironment(
        this IServiceCollection services,
        IHostEnvironment environment,
        Action<TrustedCertificateStoreOptions> configureProductionOptions,
        Action<CertificateStoreBuilder>? configureTestBuilder = null
    )
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            // Use test certificate store for development and testing
            if (configureTestBuilder != null)
            {
                return services.AddTestCertificateStore(configureTestBuilder);
            }
            else
            {
                return services.AddTestCertificateStore();
            }
        }
        else
        {
            // Use production certificate store with custom options
            return services.AddCertificateStore(configureProductionOptions);
        }
    }
}
