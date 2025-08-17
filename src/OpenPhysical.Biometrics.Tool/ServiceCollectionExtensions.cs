using OpenPhysical.Biometrics.Cli.Commands;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Infrastructure.Services;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenPhysical.Biometrics.Cli;

/// <summary>
/// Extension methods for registering FaceOFFx CLI services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all FaceOFFx services.
    /// </summary>
    [UsedImplicitly]
    public static IServiceCollection AddFaceOffxCli(this IServiceCollection services)
    {
        // Note: Cannot log during service registration as services aren't built yet

        // Register core services as Scoped to avoid early instantiation
        services.AddScoped<IFaceDetector>(sp =>
        {
            var detectorLogger = sp.GetRequiredService<ILogger<RetinaFaceDetector>>();
            return new RetinaFaceDetector(detectorLogger);
        });

        services.AddScoped<ILandmarkExtractor>(sp =>
        {
            var extractorLogger = sp.GetRequiredService<ILogger<OnnxLandmarkExtractor>>();
            return new OnnxLandmarkExtractor(extractorLogger);
        });

        // Use Transient for Jpeg2000EncoderService to avoid static cleanup issues
        services.AddTransient<IJpeg2000Encoder>(sp =>
        {
            var encoderLogger = sp.GetRequiredService<ILogger<Jpeg2000EncoderService>>();
            return new Jpeg2000EncoderService(encoderLogger);
        });

        // Register quality assessor
        services.AddScoped<IQualityAssessor>(sp =>
        {
            var assessorLogger = sp.GetRequiredService<ILogger<QualityAssessor>>();
            return new QualityAssessor(assessorLogger);
        });

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
