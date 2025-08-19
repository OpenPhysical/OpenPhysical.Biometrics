using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenPhysical.Biometrics.Infrastructure.Extensions;
using OpenPhysical.Biometrics.Tests.Common;

namespace OpenPhysical.Biometrics.Infrastructure.Tests;

/// <summary>
/// Base class for biometric integration tests that need full biometric services
/// </summary>
public abstract class BiometricIntegrationTestBase : IntegrationTestBase
{
    /// <inheritdoc/>
    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        // Add biometric configuration
        services.AddBiometricConfiguration();
        
        // Add biometric services
        services.AddBiometricServices();
    }
}