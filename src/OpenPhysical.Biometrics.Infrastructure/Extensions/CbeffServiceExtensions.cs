namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff;
using ICbeffFormatRegistry = OpenPhysical.Biometrics.Core.Cbeff.Formats.ICbeffFormatRegistry;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

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
        // Register the format registry as singleton
        services.AddSingleton<ICbeffFormatRegistry>(provider =>
        {
            var registry = new CbeffFormatRegistry();
            
            // Register PIV facial format
            var pivFacialFormat = new PivFacialFormat();
            registry.RegisterFormat(
                pivFacialFormat.PatronFormat,
                pivFacialFormat.BdbFormatOwner,
                pivFacialFormat.BdbFormatType,
                pivFacialFormat);
            
            // Register PIV fingerprint format
            var pivFingerprintFormat = new PivFingerprintFormat();
            registry.RegisterFormat(
                pivFingerprintFormat.PatronFormat,
                pivFingerprintFormat.BdbFormatOwner,
                pivFingerprintFormat.BdbFormatType,
                pivFingerprintFormat);
            
            // Add other formats here as they are implemented
            
            return registry;
        });
        
        // Register basic serializer
        services.AddSingleton<ICbeffSerializer, CbeffSerializer>();
        
        // Register extended serializer
        services.AddSingleton<ICbeffSerializerExtended, CbeffSerializerExtended>();
        
        // Register CBEFF services
        services.AddScoped<ICbeffEncoder, CbeffEncoderService>();
        services.AddScoped<ICbeffDecoder, CbeffDecoderService>();
        services.AddScoped<ICbeffSigner, CbeffSignerService>();
        services.AddScoped<ICbeffVerifier, CbeffVerifierService>();
        services.AddScoped<ICbeffBuilder, CbeffBuilderService>();
        
        return services;
    }
}