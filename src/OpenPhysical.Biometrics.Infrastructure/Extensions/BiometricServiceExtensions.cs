using System.Drawing;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Infrastructure.Pipeline.Checkpoints;
using OpenPhysical.Biometrics.Infrastructure.Pipeline.Steps;
using OpenPhysical.Biometrics.Infrastructure.Services;
using Spectre.Console;

namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering biometric services.
/// </summary>
[PublicAPI]
public static class BiometricServiceExtensions
{
    /// <summary>
    /// Registers all biometric services with default configuration.
    /// </summary>
    public static IServiceCollection AddBiometricServices(this IServiceCollection services)
    {
        // Core processing services
        services.AddSingleton<IFaceDetector, RetinaFaceDetector>();
        services.AddSingleton<ILandmarkExtractor, OnnxLandmarkExtractor>();
        services.AddSingleton<IJpeg2000Encoder, Jpeg2000EncoderService>();
        services.AddSingleton<IQualityAssessor, QualityAssessor>();

        // Library services
        services.AddSingleton<IImageProcessingService, ImageProcessingService>();
        services.AddSingleton<IJpeg2000Service, Jpeg2000Service>();
        services.AddSingleton<IBiometricPipelineService, BiometricPipelineService>();
        services.AddSingleton<IBiometricEncodingService, BiometricEncodingService>();
        services.AddSingleton<IBiometricProcessingService, BiometricProcessingService>();

        // Legacy service with new architecture support
        services.AddTransient<FacialImageEncoderService>(provider =>
        {
            var faceDetector = provider.GetRequiredService<IFaceDetector>();
            var landmarkExtractor = provider.GetRequiredService<ILandmarkExtractor>();
            var jpeg2000Encoder = provider.GetRequiredService<IJpeg2000Encoder>();
            var processingService = provider.GetRequiredService<IBiometricProcessingService>();
            var logger = provider.GetRequiredService<ILogger<FacialImageEncoderService>>();

            return new FacialImageEncoderService(
                faceDetector,
                landmarkExtractor,
                jpeg2000Encoder,
                processingService,
                logger
            );
        });

        // Pipeline steps
        services.AddTransient<BiometricPipelineSteps.IFaceDetectionStep, FaceDetectionStep>();
        services.AddTransient<
            BiometricPipelineSteps.ILandmarkExtractionStep,
            LandmarkExtractionStep
        >();
        services.AddTransient<
            BiometricPipelineSteps.IQualityAssessmentStep,
            QualityAssessmentStep
        >();
        services.AddTransient<
            BiometricPipelineSteps.IImageTransformStep,
            ImageTransformationStep
        >();
        services.AddTransient<BiometricPipelineSteps.IJpeg2000EncodingStep, EncodingStep>();

        // Checkpoints
        services.AddTransient<ICheckpoint<Core.Domain.Pipeline.QualityMetrics>>(
            provider => new ConsoleQualityApprovalCheckpoint(
                AnsiConsole.Console,
                provider.GetRequiredService<ILogger<QualityApprovalCheckpoint>>()
            )
        );

        services.AddTransient<ICheckpoint<IReadOnlyList<Core.Domain.Detection.DetectedFace>>>(
            provider => new ConsoleFaceSelectionCheckpoint(
                AnsiConsole.Console,
                provider.GetRequiredService<ILogger<FaceSelectionCheckpoint>>()
            )
        );

        // CBEFF services - use the static method from CbeffServiceExtensions
        CbeffServiceExtensions.AddCbeffServices(services);

        return services;
    }

    /// <summary>
    /// Registers biometric services with custom configuration.
    /// </summary>
    public static IServiceCollection AddBiometricServices(
        this IServiceCollection services,
        Action<BiometricServiceOptions> configureOptions
    )
    {
        var options = new BiometricServiceOptions();
        configureOptions(options);

        // Register based on options
        if (options.UseMockDetector)
        {
            services.AddSingleton<IFaceDetector>(provider => new MockFaceDetector(
                provider.GetRequiredService<ILogger<MockFaceDetector>>()
            ));
        }
        else
        {
            services.AddSingleton<IFaceDetector, RetinaFaceDetector>();
        }

        // Always register these services
        services.AddSingleton<ILandmarkExtractor, OnnxLandmarkExtractor>();
        services.AddSingleton<IJpeg2000Encoder, Jpeg2000EncoderService>();
        services.AddSingleton<IQualityAssessor, QualityAssessor>();

        // Library services
        services.AddSingleton<IImageProcessingService, ImageProcessingService>();
        services.AddSingleton<IJpeg2000Service, Jpeg2000Service>();
        services.AddSingleton<IBiometricPipelineService, BiometricPipelineService>();
        services.AddSingleton<IBiometricEncodingService, BiometricEncodingService>();
        services.AddSingleton<IBiometricProcessingService, BiometricProcessingService>();

        // Legacy service with new architecture support
        services.AddTransient<FacialImageEncoderService>(provider =>
        {
            var faceDetector = provider.GetRequiredService<IFaceDetector>();
            var landmarkExtractor = provider.GetRequiredService<ILandmarkExtractor>();
            var jpeg2000Encoder = provider.GetRequiredService<IJpeg2000Encoder>();
            var processingService = provider.GetRequiredService<IBiometricProcessingService>();
            var logger = provider.GetRequiredService<ILogger<FacialImageEncoderService>>();

            return new FacialImageEncoderService(
                faceDetector,
                landmarkExtractor,
                jpeg2000Encoder,
                processingService,
                logger
            );
        });

        // Pipeline steps with custom configuration
        if (options.RegisterPipelineSteps)
        {
            services.AddTransient<BiometricPipelineSteps.IFaceDetectionStep>(
                provider => new FaceDetectionStep(
                    provider.GetRequiredService<IFaceDetector>(),
                    provider.GetRequiredService<ILogger<FaceDetectionStep>>(),
                    options.MinFaceConfidence
                )
            );

            services.AddTransient<
                BiometricPipelineSteps.ILandmarkExtractionStep,
                LandmarkExtractionStep
            >();

            services.AddTransient<BiometricPipelineSteps.IQualityAssessmentStep>(
                provider => new QualityAssessmentStep(
                    provider.GetRequiredService<IQualityAssessor>(),
                    provider.GetRequiredService<ILogger<QualityAssessmentStep>>(),
                    options.QualityStandard,
                    Core.Domain.Quality.QualityScore.Create(options.MinQualityThreshold).Value
                )
            );

            services.AddTransient<
                BiometricPipelineSteps.IImageTransformStep,
                ImageTransformationStep
            >();
            services.AddTransient<BiometricPipelineSteps.IJpeg2000EncodingStep, EncodingStep>();
        }

        // Checkpoints
        if (options.RegisterCheckpoints)
        {
            services.AddTransient<ICheckpoint<Core.Domain.Pipeline.QualityMetrics>>(
                provider => new ConsoleQualityApprovalCheckpoint(
                    AnsiConsole.Console,
                    provider.GetRequiredService<ILogger<QualityApprovalCheckpoint>>()
                )
            );

            services.AddTransient<ICheckpoint<IReadOnlyList<Core.Domain.Detection.DetectedFace>>>(
                provider => new ConsoleFaceSelectionCheckpoint(
                    AnsiConsole.Console,
                    provider.GetRequiredService<ILogger<FaceSelectionCheckpoint>>()
                )
            );
        }

        // CBEFF services if requested
        if (options.RegisterCbeffServices)
        {
            CbeffServiceExtensions.AddCbeffServices(services);
        }

        return services;
    }

    /// <summary>
    /// Registers only the core biometric processing services without pipeline infrastructure.
    /// </summary>
    public static IServiceCollection AddBiometricCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IFaceDetector, RetinaFaceDetector>();
        services.AddSingleton<ILandmarkExtractor, OnnxLandmarkExtractor>();
        services.AddSingleton<IJpeg2000Encoder, Jpeg2000EncoderService>();
        services.AddSingleton<IQualityAssessor, QualityAssessor>();

        // Functional facial image encoder service (Result<T> pattern)
        services.AddTransient<FacialImageEncoderService>();

        // Aggregated biometric processing services for convenience
        services.AddTransient<BiometricProcessingServices>();

        return services;
    }

    /// <summary>
    /// Registers only the library services without core dependencies.
    /// </summary>
    public static IServiceCollection AddBiometricLibraryServices(this IServiceCollection services)
    {
        services.AddSingleton<IImageProcessingService, ImageProcessingService>();
        services.AddSingleton<IJpeg2000Service, Jpeg2000Service>();
        services.AddSingleton<IBiometricPipelineService, BiometricPipelineService>();

        return services;
    }

    /// <summary>
    /// Mock face detector for testing.
    /// </summary>
    private sealed class MockFaceDetector : IFaceDetector
    {
        private readonly ILogger<MockFaceDetector> _logger;

        public MockFaceDetector(ILogger<MockFaceDetector> logger)
        {
            _logger = logger;
        }

        public Task<Result<IReadOnlyList<Core.Domain.Detection.DetectedFace>>> DetectFacesAsync(
            RgbImage image,
            CancellationToken cancellationToken = default
        )
        {
            _logger.LogDebug(
                new EventId(401900, "BiometricServiceExtensions"),
                "Mock face detection returning single face"
            );

            var faceBox = Core
                .Domain.Detection.FaceBox.Create(
                    image.Width / 4f,
                    image.Height / 4f,
                    image.Width / 2f,
                    image.Height / 2f
                )
                .Value;

            var faces = new List<Core.Domain.Detection.DetectedFace> { new(faceBox, 0.95f) };

            return Task.FromResult(
                Result.Success<IReadOnlyList<Core.Domain.Detection.DetectedFace>>(faces)
            );
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}

/// <summary>
/// Options for biometric service registration.
/// </summary>
[PublicAPI]
public sealed class BiometricServiceOptions
{
    /// <summary>
    /// Gets or sets whether to use mock face detector for testing.
    /// </summary>
    public bool UseMockDetector { get; set; }

    /// <summary>
    /// Gets or sets whether to register pipeline steps.
    /// </summary>
    public bool RegisterPipelineSteps { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to register checkpoints.
    /// </summary>
    public bool RegisterCheckpoints { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to register CBEFF services.
    /// </summary>
    public bool RegisterCbeffServices { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum face confidence for detection.
    /// </summary>
    public float MinFaceConfidence { get; set; } = 0.8f;

    /// <summary>
    /// Gets or sets the quality assessment standard.
    /// </summary>
    public QualityStandard QualityStandard { get; set; } = QualityStandard.NistSp800_76;

    /// <summary>
    /// Gets or sets the minimum quality threshold.
    /// </summary>
    public float MinQualityThreshold { get; set; } = 0.7f;
}
