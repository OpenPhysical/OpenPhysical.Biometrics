namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Core.Domain;
using OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;
using System.Linq;
using System.Reflection;

/// <summary>
/// Extension methods for automatically discovering and registering biometric format handlers.
/// </summary>
public static class BiometricFormatRegistration
{
    /// <summary>
    /// Automatically discovers and registers all biometric format handlers decorated with BiometricFormatAttribute.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan for format handlers.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBiometricFormats(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = new[] { Assembly.GetExecutingAssembly() };
        }
        
        // Register the generic registry with auto-initialization
        services.AddSingleton<IBiometricFormatRegistry>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<GenericBiometricFormatRegistry>>();
            var registry = new GenericBiometricFormatRegistry(Maybe<ILogger<GenericBiometricFormatRegistry>>.From(logger));
            
            logger.LogDebug("Auto-initializing biometric format registry during singleton creation");
            logger.LogDebug("Registry will discover handlers from {AssemblyCount} assemblies", assemblies.Length);
            
            // Register all format handlers immediately during registry creation
            var formatRegistrations = provider.GetServices<IFormatHandlerRegistration>().ToList();
            logger.LogDebug("Found {Count} format handler registrations to register", formatRegistrations.Count);
            foreach (var registration in formatRegistrations)
            {
                registration.Register(registry);
            }
            
            // Register all modality handlers
            var modalityRegistrations = provider.GetServices<IModalityHandlerRegistration>().ToList();
            logger.LogDebug("Found {Count} modality handler registrations to register", modalityRegistrations.Count);
            foreach (var registration in modalityRegistrations)
            {
                registration.Register(registry);
            }
            
            logger.LogDebug("Auto-initialization complete during singleton creation");
            return registry;
        });
        
        // Discover and register format handlers
        var formatHandlerTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetCustomAttribute<BiometricFormatAttribute>() != null)
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .ToList();
            
        // Discover and register modality handlers
        var modalityHandlerTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetInterfaces().Any(i => 
                i.IsGenericType && 
                i.GetGenericTypeDefinition() == typeof(IBiometricModalityHandler<,>) &&
                i.GetGenericArguments()[1] == typeof(ExtractedBiometricData)))
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .ToList();
            
        // Log discovery statistics during initialization
        // Log handler discovery statistics
        services.AddSingleton<IHandlerDiscoveryService>(provider =>
        {
            var logger = Maybe<ILogger<HandlerDiscoveryService>>.From(provider.GetService<ILogger<HandlerDiscoveryService>>());
            return new HandlerDiscoveryService(formatHandlerTypes, modalityHandlerTypes, logger);
        });
            
        foreach (var handlerType in formatHandlerTypes)
        {
            // Register the handler as a service
            services.AddSingleton(handlerType);
            
            // Find the generic interface type
            var interfaceType = handlerType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBiometricFormatHandler<>));
                
            if (interfaceType != null)
            {
                var domainType = interfaceType.GetGenericArguments()[0];
                services.AddSingleton(interfaceType, handlerType);
                
                // Register with the registry during service provider build
                services.AddSingleton<IFormatHandlerRegistration>(provider =>
                    new FormatHandlerRegistration(handlerType, domainType, provider));
            }
        }
            
        foreach (var handlerType in modalityHandlerTypes)
        {
            // Register the handler as a service
            services.AddSingleton(handlerType);
            
            // Find the generic interface type
            var interfaceType = handlerType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && 
                    i.GetGenericTypeDefinition() == typeof(IBiometricModalityHandler<,>) &&
                    i.GetGenericArguments()[1] == typeof(ExtractedBiometricData));
                    
            if (interfaceType != null)
            {
                var domainType = interfaceType.GetGenericArguments()[0];
                services.AddSingleton(interfaceType, handlerType);
                
                // Register with the registry during service provider build
                services.AddSingleton<IModalityHandlerRegistration>(provider =>
                    new ModalityHandlerRegistration(handlerType, domainType, provider));
            }
        }
        
        // Register the initialization service
        services.AddSingleton<IBiometricRegistryInitializer, BiometricRegistryInitializer>();
        
        return services;
    }
    
    /// <summary>
    /// Ensures the biometric registry is initialized with all discovered handlers.
    /// Call this during application startup after the service provider is built.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The service provider for chaining.</returns>
    public static IServiceProvider InitializeBiometricRegistry(this IServiceProvider serviceProvider)
    {
        // Registry auto-initializes during singleton creation, this is just for explicit initialization if needed
        var registry = serviceProvider.GetRequiredService<IBiometricFormatRegistry>();
        return serviceProvider;
    }
}

/// <summary>
/// Interface for registry initialization.
/// </summary>
public interface IBiometricRegistryInitializer
{
    /// <summary>
    /// Initializes the registry with all discovered handlers.
    /// </summary>
    void Initialize();
}

/// <summary>
/// Interface for format handler registration.
/// </summary>
public interface IFormatHandlerRegistration
{
    /// <summary>
    /// Registers the format handler with the registry.
    /// </summary>
    void Register(IBiometricFormatRegistry registry);
}

/// <summary>
/// Interface for modality handler registration.
/// </summary>
public interface IModalityHandlerRegistration
{
    /// <summary>
    /// Registers the modality handler with the registry.
    /// </summary>
    void Register(IBiometricFormatRegistry registry);
}

internal sealed class BiometricRegistryInitializer : IBiometricRegistryInitializer
{
    private readonly IServiceProvider _serviceProvider;
    
    public BiometricRegistryInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public void Initialize()
    {
        var registry = _serviceProvider.GetRequiredService<IBiometricFormatRegistry>();
        
        // Register all format handlers
        var formatRegistrations = _serviceProvider.GetServices<IFormatHandlerRegistration>();
        foreach (var registration in formatRegistrations)
        {
            registration.Register(registry);
        }
        
        // Register all modality handlers
        var modalityRegistrations = _serviceProvider.GetServices<IModalityHandlerRegistration>();
        foreach (var registration in modalityRegistrations)
        {
            registration.Register(registry);
        }
    }
}

internal sealed class FormatHandlerRegistration : IFormatHandlerRegistration
{
    private readonly Type _handlerType;
    private readonly Type _domainType;
    private readonly IServiceProvider _serviceProvider;
    
    public FormatHandlerRegistration(Type handlerType, Type domainType, IServiceProvider serviceProvider)
    {
        _handlerType = handlerType;
        _domainType = domainType;
        _serviceProvider = serviceProvider;
    }
    
    public void Register(IBiometricFormatRegistry registry)
    {
        var handler = _serviceProvider.GetRequiredService(_handlerType);
        
        // Get logger from service provider for registration logging
        var logger = _serviceProvider.GetService<ILogger<FormatHandlerRegistration>>();
        logger?.LogInformation("Registering format handler {HandlerType} for domain type {DomainType}", 
            _handlerType.Name, _domainType.Name);
        
        // Use reflection to call the generic RegisterFormatHandler method
        var registerMethod = typeof(IBiometricFormatRegistry)
            .GetMethod(nameof(IBiometricFormatRegistry.RegisterFormatHandler))!
            .MakeGenericMethod(_domainType);
            
        var result = registerMethod.Invoke(registry, new[] { handler });
        
        // Check if registration failed (Result<T> type)
        if (result != null)
        {
            var resultType = result.GetType();
            var isSuccessProperty = resultType.GetProperty("IsSuccess");
            var errorProperty = resultType.GetProperty("Error");
            
            if (isSuccessProperty != null && errorProperty != null)
            {
                var isSuccess = (bool)isSuccessProperty.GetValue(result)!;
                if (!isSuccess)
                {
                    var error = errorProperty.GetValue(result)?.ToString() ?? "Unknown error";
                    logger?.LogError("Failed to register format handler {HandlerType}: {Error}", 
                        _handlerType.Name, error);
                    throw new InvalidOperationException($"Failed to register format handler {_handlerType.Name}: {error}");
                }
                else
                {
                    logger?.LogInformation("Successfully registered format handler {HandlerType} for domain type {DomainType}", 
                        _handlerType.Name, _domainType.Name);
                }
            }
        }
    }
}

internal sealed class ModalityHandlerRegistration : IModalityHandlerRegistration
{
    private readonly Type _handlerType;
    private readonly Type _domainType;
    private readonly IServiceProvider _serviceProvider;
    
    public ModalityHandlerRegistration(Type handlerType, Type domainType, IServiceProvider serviceProvider)
    {
        _handlerType = handlerType;
        _domainType = domainType;
        _serviceProvider = serviceProvider;
    }
    
    public void Register(IBiometricFormatRegistry registry)
    {
        var handler = _serviceProvider.GetRequiredService(_handlerType);
        var logger = _serviceProvider.GetService<ILogger<ModalityHandlerRegistration>>();
        
        logger?.LogInformation("Registering modality handler {HandlerType} for domain type {DomainType}", 
            _handlerType.Name, _domainType.Name);
        
        // Use reflection to call the generic RegisterModalityHandler method
        var registerMethod = typeof(IBiometricFormatRegistry)
            .GetMethod(nameof(IBiometricFormatRegistry.RegisterModalityHandler))!
            .MakeGenericMethod(_domainType);
            
        var result = registerMethod.Invoke(registry, new[] { handler });
        
        // Check if registration failed (Result<T> type)
        if (result != null)
        {
            var resultType = result.GetType();
            var isSuccessProperty = resultType.GetProperty("IsSuccess");
            var errorProperty = resultType.GetProperty("Error");
            
            if (isSuccessProperty != null && errorProperty != null)
            {
                var isSuccess = (bool)isSuccessProperty.GetValue(result)!;
                if (!isSuccess)
                {
                    var error = errorProperty.GetValue(result)?.ToString() ?? "Unknown error";
                    logger?.LogError("Failed to register modality handler {HandlerType}: {Error}", 
                        _handlerType.Name, error);
                    throw new InvalidOperationException($"Failed to register modality handler {_handlerType.Name}: {error}");
                }
                else
                {
                    logger?.LogInformation("Successfully registered modality handler {HandlerType} for domain type {DomainType}", 
                        _handlerType.Name, _domainType.Name);
                }
            }
        }
    }
}

/// <summary>
/// Service for logging handler discovery statistics.
/// </summary>
public interface IHandlerDiscoveryService
{
    /// <summary>
    /// Gets the number of discovered format handlers.
    /// </summary>
    int FormatHandlerCount { get; }
    
    /// <summary>
    /// Gets the number of discovered modality handlers.
    /// </summary>
    int ModalityHandlerCount { get; }
}

internal sealed class HandlerDiscoveryService : IHandlerDiscoveryService
{
    public int FormatHandlerCount { get; }
    public int ModalityHandlerCount { get; }
    
    public HandlerDiscoveryService(
        IReadOnlyList<Type> formatHandlerTypes, 
        IReadOnlyList<Type> modalityHandlerTypes,
        Maybe<ILogger<HandlerDiscoveryService>> logger)
    {
        FormatHandlerCount = formatHandlerTypes.Count;
        ModalityHandlerCount = modalityHandlerTypes.Count;
        
        logger.Execute(log =>
        {
            log.LogDebug("Discovered {FormatCount} format handler types: {FormatTypes}", 
                formatHandlerTypes.Count, 
                string.Join(", ", formatHandlerTypes.Select(t => t.Name)));
            log.LogDebug("Discovered {ModalityCount} modality handler types: {ModalityTypes}", 
                modalityHandlerTypes.Count, 
                string.Join(", ", modalityHandlerTypes.Select(t => t.Name)));
        });
    }
}