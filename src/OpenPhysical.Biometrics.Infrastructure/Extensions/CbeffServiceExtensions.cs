namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;
using OpenPhysical.Biometrics.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering CBEFF services.
/// </summary>
public static class CbeffServiceExtensions
{
    /// <summary>
    /// Adds CBEFF serialization services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCbeffServices(this IServiceCollection services)
    {
        // Add the new generic biometric format registry with auto-discovery
        services.AddBiometricFormats(Assembly.GetExecutingAssembly());

        // Register basic serializer
        services.AddSingleton<ICbeffSerializer, CbeffSerializer>();

        // Register extended serializer
        services.AddSingleton<ICbeffSerializerExtended, CbeffSerializerExtended>();

        // Register CBEFF services
        services.AddScoped<ICbeffEncoder, CbeffEncoderService>();
        services.AddScoped<ICbeffDecoder, CbeffDecoderService>();
        services.AddScoped<ICbeffBuilder, CbeffBuilderService>();

        // Register PIV cross-validation services
        services.AddScoped<IChuidParsingService, ChuidParsingService>();

        return services;
    }
}
