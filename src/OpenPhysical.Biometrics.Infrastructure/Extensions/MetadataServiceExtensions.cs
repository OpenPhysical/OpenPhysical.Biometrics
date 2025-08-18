namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Abstractions.Metadata;
using OpenPhysical.Biometrics.Infrastructure.Metadata;

/// <summary>
/// Extension methods for registering metadata services.
/// </summary>
public static class MetadataServiceExtensions
{
    /// <summary>
    /// Adds metadata extraction services to the service collection.
    /// </summary>
    public static IServiceCollection AddMetadataServices(this IServiceCollection services)
    {
        // Register extractors
        services.AddSingleton<IJpegMetadataExtractor, JpegMetadataExtractor>();
        services.AddSingleton<IImageMetadataExtractor>(provider => 
            provider.GetRequiredService<IJpegMetadataExtractor>());
        
        // Register transformer
        services.AddSingleton<IMetadataTransformer, MetadataTransformer>();
        
        return services;
    }
}