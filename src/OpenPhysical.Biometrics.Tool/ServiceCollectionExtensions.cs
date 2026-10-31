using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Infrastructure.Extensions;
using OpenPhysical.Biometrics.Infrastructure.Services;
using OpenPhysical.Biometrics.Tool.Commands;

namespace OpenPhysical.Biometrics.Tool;

/// <summary>
/// Extension methods for registering Biometrics Tool services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Biometrics Tool services.
    /// </summary>
    [UsedImplicitly]
    public static IServiceCollection AddBiometricsTool(this IServiceCollection services)
    {
        services.AddBiometricServices();

        // Register CLI commands
        services.AddTransient<ProcessCommand>();
        services.AddTransient<RoiCommand>();
        services.AddTransient<QualityCommand>();
        services.AddTransient<ValidateCommand>();
        services.AddTransient<InteractiveCommand>();
        services.AddTransient<DatasetCommand>();
        services.AddTransient<DatasetValidateCommand>();
        services.AddTransient<DatasetReviewCommand>();

        return services;
    }
}
