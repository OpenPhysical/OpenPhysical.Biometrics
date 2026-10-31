namespace OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Core.Domain;
using OpenPhysical.Biometrics.Infrastructure.Extensions;

/// <summary>
/// A lazy-initializing wrapper around the generic biometric format registry that defers
/// initialization until first access to avoid multiple service provider issues.
/// </summary>
public sealed class LazyInitializationBiometricFormatRegistry : IBiometricFormatRegistry
{
    private readonly GenericBiometricFormatRegistry _innerRegistry = new();
    private volatile bool _isInitialized = false;
    private readonly object _initializationLock = new();

    /// <summary>
    /// Ensures the registry is initialized with the given service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to use for initialization.</param>
    public void EnsureInitialized(IServiceProvider serviceProvider)
    {
        if (_isInitialized)
            return;

        lock (_initializationLock)
        {
            if (_isInitialized)
                return;

            Console.WriteLine("DEBUG: Lazy initializing biometric format registry");

            var initializer = serviceProvider.GetRequiredService<IBiometricRegistryInitializer>();

            // Temporarily replace the registry in the initializer to use our inner registry
            var initializerType = initializer.GetType();
            var serviceProviderField = initializerType.GetField(
                "_serviceProvider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

            if (serviceProviderField != null)
            {
                // Create a custom initializer that targets our inner registry
                var customInitializer = new CustomBiometricRegistryInitializer(
                    serviceProvider,
                    _innerRegistry
                );
                customInitializer.Initialize();
            }

            _isInitialized = true;
            Console.WriteLine("DEBUG: Lazy initialization complete");
        }
    }

    /// <inheritdoc />
    public Result RegisterFormatHandler<TDomainRecord>(
        IBiometricFormatHandler<TDomainRecord> formatHandler
    )
        where TDomainRecord : class
    {
        return _innerRegistry.RegisterFormatHandler(formatHandler);
    }

    /// <inheritdoc />
    public Result RegisterModalityHandler<TDomainRecord>(
        IBiometricModalityHandler<TDomainRecord, ExtractedBiometricData> modalityHandler
    )
        where TDomainRecord : class
    {
        return _innerRegistry.RegisterModalityHandler(modalityHandler);
    }

    /// <inheritdoc />
    public Maybe<object> GetFormatHandler(
        byte patronFormat,
        ushort bdbFormatOwner,
        ushort bdbFormatType
    )
    {
        // This method will be called when the registry is actually used, so initialize here
        // We can't get the service provider here, so this approach won't work
        throw new InvalidOperationException(
            "Registry must be initialized before use. Call EnsureInitialized() first."
        );
    }

    /// <inheritdoc />
    public Maybe<
        IBiometricModalityHandler<TDomainRecord, ExtractedBiometricData>
    > GetModalityHandler<TDomainRecord>()
        where TDomainRecord : class
    {
        if (!_isInitialized)
            throw new InvalidOperationException(
                "Registry must be initialized before use. Call EnsureInitialized() first."
            );

        return _innerRegistry.GetModalityHandler<TDomainRecord>();
    }

    /// <inheritdoc />
    public Result<BiometricExtraction> ProcessBiometricData(
        byte patronFormat,
        ushort bdbFormatOwner,
        ushort bdbFormatType,
        ImmutableArray<byte> data
    )
    {
        if (!_isInitialized)
            throw new InvalidOperationException(
                "Registry must be initialized before use. Call EnsureInitialized() first."
            );

        return _innerRegistry.ProcessBiometricData(
            patronFormat,
            bdbFormatOwner,
            bdbFormatType,
            data
        );
    }

    /// <inheritdoc />
    public ImmutableList<BiometricFormatInfo> GetRegisteredFormats()
    {
        return _innerRegistry.GetRegisteredFormats();
    }
}

/// <summary>
/// Custom initializer that targets a specific registry instance.
/// </summary>
internal sealed class CustomBiometricRegistryInitializer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBiometricFormatRegistry _targetRegistry;

    public CustomBiometricRegistryInitializer(
        IServiceProvider serviceProvider,
        IBiometricFormatRegistry targetRegistry
    )
    {
        _serviceProvider = serviceProvider;
        _targetRegistry = targetRegistry;
    }

    public void Initialize()
    {
        // Register all format handlers
        var formatRegistrations = _serviceProvider.GetServices<IFormatHandlerRegistration>();
        foreach (var registration in formatRegistrations)
        {
            registration.Register(_targetRegistry);
        }

        // Register all modality handlers
        var modalityRegistrations = _serviceProvider.GetServices<IModalityHandlerRegistration>();
        foreach (var registration in modalityRegistrations)
        {
            registration.Register(_targetRegistry);
        }
    }
}
