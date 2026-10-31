using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Infrastructure.Pipeline;
using OpenPhysical.Biometrics.Infrastructure.Pipeline.Checkpoints;
using OpenPhysical.Biometrics.Infrastructure.Pipeline.Steps;
using OpenPhysical.Biometrics.Infrastructure.Services;
using static OpenPhysical.Biometrics.Core.Domain.Pipeline.BiometricPipelineSteps;

namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering biometric pipeline services.
/// </summary>
[PublicAPI]
public static class PipelineServiceExtensions
{
    /// <summary>
    /// Adds biometric pipeline services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddBiometricPipelines(this IServiceCollection services)
    {
        // Register pipeline context
        services.AddScoped<IPipelineContext>(provider => PipelineContext.Create());

        // Register pipeline steps
        services.AddScoped<IFaceDetectionStep, FaceDetectionStep>();
        services.AddScoped<IQualityAssessmentStep, QualityAssessmentStep>();
        services.AddScoped<ILandmarkExtractionStep, LandmarkExtractionStep>();
        services.AddScoped<IImageTransformStep, ImageTransformationStep>();
        services.AddScoped<IJpeg2000EncodingStep, EncodingStep>();

        // Register pipeline service
        services.AddScoped<IBiometricPipelineService, BiometricPipelineService>();

        // Register composite pipeline
        services.AddScoped<
            IBiometricPipeline<BiometricData, ProcessedBiometricData>,
            CompositeBiometricPipeline<BiometricData, ProcessedBiometricData>
        >();

        return services;
    }

    /// <summary>
    /// Adds biometric pipeline services with custom configuration.
    /// </summary>
    public static IServiceCollection AddBiometricPipelines(
        this IServiceCollection services,
        Action<BiometricPipelineDefaults> configureDefaults
    )
    {
        services.Configure<BiometricPipelineDefaults>(configureDefaults);
        return services.AddBiometricPipelines();
    }

    /// <summary>
    /// Adds interactive checkpoint support for human-in-the-middle workflows.
    /// </summary>
    public static IServiceCollection AddInteractiveCheckpoints(this IServiceCollection services)
    {
        services.AddScoped<ICheckpoint<QualityMetrics>, QualityApprovalCheckpoint>();
        services.AddScoped<ICheckpoint<IReadOnlyList<DetectedFace>>, FaceSelectionCheckpoint>();

        return services;
    }

    /// <summary>
    /// Adds console-based checkpoint handlers for CLI applications.
    /// </summary>
    public static IServiceCollection AddConsoleCheckpoints(this IServiceCollection services)
    {
        services.AddInteractiveCheckpoints();

        // Override with console-specific implementations
        services.AddScoped<ICheckpoint<QualityMetrics>, ConsoleQualityApprovalCheckpoint>();
        services.AddScoped<
            ICheckpoint<IReadOnlyList<DetectedFace>>,
            ConsoleFaceSelectionCheckpoint
        >();

        return services;
    }
}

/// <summary>
/// Configuration defaults for biometric pipeline services.
/// Provides default processing standards and execution policies for pipeline operations.
/// </summary>
[PublicAPI]
public sealed class BiometricPipelineDefaults
{
    /// <summary>
    /// Gets or sets the default biometric processing standards.
    /// </summary>
    public BiometricProcessingStandards DefaultStandards { get; set; } =
        BiometricProcessingStandards.Piv;

    /// <summary>
    /// Gets or sets the default pipeline execution policy.
    /// </summary>
    public PipelineExecutionPolicy DefaultPolicy { get; set; } =
        PipelineExecutionPolicy.Interactive;

    /// <summary>
    /// Creates defaults suitable for PIV processing.
    /// </summary>
    public static BiometricPipelineDefaults ForPiv() =>
        new()
        {
            DefaultStandards = BiometricProcessingStandards.Piv,
            DefaultPolicy = PipelineExecutionPolicy.Production,
        };

    /// <summary>
    /// Creates defaults suitable for ICAO passport processing.
    /// </summary>
    public static BiometricPipelineDefaults ForIcao() =>
        new()
        {
            DefaultStandards = BiometricProcessingStandards.Icao,
            DefaultPolicy = PipelineExecutionPolicy.Production,
        };

    /// <summary>
    /// Creates defaults suitable for interactive development.
    /// </summary>
    public static BiometricPipelineDefaults ForDevelopment() =>
        new()
        {
            DefaultStandards = BiometricProcessingStandards.Lenient,
            DefaultPolicy = PipelineExecutionPolicy.Debug,
        };
}
