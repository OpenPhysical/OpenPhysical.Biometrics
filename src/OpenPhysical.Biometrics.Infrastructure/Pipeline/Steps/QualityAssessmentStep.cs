using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static OpenPhysical.Biometrics.Core.Domain.Pipeline.BiometricPipelineSteps;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline.Steps;

/// <summary>
/// Pipeline step that assesses the quality of biometric data using configurable standards.
/// </summary>
[PublicAPI]
public sealed class QualityAssessmentStep : IQualityAssessmentStep
{
    private readonly IQualityAssessor _qualityAssessor;
    private readonly ILogger<QualityAssessmentStep> _logger;

    /// <summary>
    /// Gets the unique identifier for this pipeline step.
    /// </summary>
    public string StepId => "quality-assessment";

    /// <summary>
    /// Gets the human-readable name of this step.
    /// </summary>
    public string Name => "Quality Assessment";

    /// <summary>
    /// Gets the description of what this step does.
    /// </summary>
    public string Description => "Assesses biometric data quality according to specified standards";

    /// <summary>
    /// Gets the quality standards used for assessment.
    /// </summary>
    public QualityStandard QualityStandard { get; }

    /// <summary>
    /// Gets the minimum acceptable quality threshold.
    /// </summary>
    public QualityScore MinQualityThreshold { get; }

    /// <summary>
    /// Initializes a new instance of the quality assessment step.
    /// </summary>
    public QualityAssessmentStep(
        IQualityAssessor qualityAssessor,
        ILogger<QualityAssessmentStep> logger,
        QualityStandard qualityStandard = QualityStandard.NistSp800_76,
        float minQualityThreshold = 0.7f)
    {
        _qualityAssessor = qualityAssessor;
        _logger = logger;
        QualityStandard = qualityStandard;
        MinQualityThreshold = QualityScore.Create(minQualityThreshold).Value;
    }

    /// <summary>
    /// Executes quality assessment on the processed biometric data.
    /// </summary>
    public async Task<Result<ProcessedBiometricData>> ExecuteAsync(
        ProcessedBiometricData input, 
        IPipelineContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting quality assessment for execution {ExecutionId}", context.ExecutionId);

        if (!CanProcess(input))
        {
            var error = $"Cannot process biometric data without detected faces for quality assessment";
            _logger.LogWarning(error);
            return Result.Failure<ProcessedBiometricData>(error);
        }

        try
        {
            // Convert biometric data to ImageSharp format for quality assessment
            using var image = Image.Load<Rgba32>(input.Data.RawData.ToArray());
            
            // Get the primary detected face for assessment
            var primaryFace = input.DetectedFaces.OrderByDescending(f => f.Confidence).First();
            
            // Check if we have landmarks, if not skip quality assessment
            if (!input.Landmarks.HasValue)
            {
                _logger.LogWarning("No landmarks available for quality assessment, skipping");
                return Result.Success(input);
            }
            
            // Perform quality assessment with face and landmarks
            var assessmentOptions = new Core.Domain.Quality.QualityAssessmentOptions();
            var assessmentResult = await _qualityAssessor.AssessAsync(image, primaryFace, input.Landmarks.Value, assessmentOptions);
            
            if (assessmentResult.IsFailure)
            {
                _logger.LogError("Quality assessment failed: {Error}", assessmentResult.Error);
                return Result.Failure<ProcessedBiometricData>(assessmentResult.Error);
            }

            var qualityResult = assessmentResult.Value;
            
            _logger.LogInformation(
                "Quality assessment completed: Overall={Overall:F3}, Sharpness={Sharpness:F3}",
                qualityResult.Overall.Value, 
                qualityResult.Sharpness.Overall.Value);

            // Check if quality meets minimum threshold
            var meetsThreshold = qualityResult.Overall.Value >= MinQualityThreshold.Value;
            
            if (!meetsThreshold)
            {
                _logger.LogWarning(
                    "Quality assessment failed: {ActualQuality:F3} < {RequiredQuality:F3}",
                    qualityResult.Overall.Value, MinQualityThreshold.Value);
            }

            // Create enhanced quality metrics
            var enhancedQuality = input.Quality
                .WithSharpness(qualityResult.Sharpness.Overall)
                .WithAdditionalMetric("symmetry", qualityResult.Symmetry.Overall)
                .WithAdditionalMetric("geometry", qualityResult.Geometry.Overall)
                .WithAdditionalMetric("sharpness_ratio", QualityScore.Create(qualityResult.Sharpness.HighFrequencyRatio).Value);

            // Update processed data with enhanced quality metrics
            var processedData = input with 
            { 
                Quality = enhancedQuality,
                ProcessingMetadata = input.ProcessingMetadata
                    .SetItem("quality_step", StepId)
                    .SetItem("quality_standard", QualityStandard.ToString())
                    .SetItem("quality_threshold", MinQualityThreshold.Value)
                    .SetItem("quality_passed", meetsThreshold)
                    .SetItem("overall_quality", qualityResult.Overall.Value)
                    .SetItem("sharpness_quality", qualityResult.Sharpness.Overall.Value)
                    .SetItem("symmetry_quality", qualityResult.Symmetry.Overall.Value)
                    .SetItem("geometry_quality", qualityResult.Geometry.Overall.Value)
                    .SetItem("violations_count", qualityResult.Violations.Count)
            };

            // If quality check fails, include that information but don't fail the step
            // Allow the pipeline to decide whether to continue or prompt for user decision
            if (!meetsThreshold)
            {
                processedData = processedData with
                {
                    ProcessingMetadata = processedData.ProcessingMetadata
                        .SetItem("quality_warning", "Quality below minimum threshold")
                };
            }

            _logger.LogDebug("Quality assessment step completed successfully");
            return Result.Success(processedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quality assessment step failed with exception");
            return Result.Failure<ProcessedBiometricData>($"Quality assessment failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines if this step can process the given input.
    /// </summary>
    public bool CanProcess(ProcessedBiometricData input)
    {
        // Can process data that has detected faces and is a facial image
        return input.DetectedFaces.Count > 0 && 
               input.Data.DataType == BiometricDataType.FacialImage;
    }
}