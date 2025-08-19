using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Biometrics.Core.Domain.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static OpenPhysical.Biometrics.Core.Domain.Pipeline.BiometricPipelineSteps;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline.Steps;

/// <summary>
/// Pipeline step that extracts facial landmarks from detected faces using ONNX models.
/// </summary>
[PublicAPI]
public sealed class LandmarkExtractionStep : ILandmarkExtractionStep
{
    private readonly ILandmarkExtractor _landmarkExtractor;
    private readonly ILogger<LandmarkExtractionStep> _logger;

    /// <summary>
    /// Gets the unique identifier for this pipeline step.
    /// </summary>
    public string StepId => "landmark-extraction";

    /// <summary>
    /// Gets the human-readable name of this step.
    /// </summary>
    public string Name => "Landmark Extraction";

    /// <summary>
    /// Gets the description of what this step does.
    /// </summary>
    public string Description => "Extracts precise facial landmarks using PFLD deep learning model";

    /// <summary>
    /// Gets the number of landmark points extracted.
    /// </summary>
    public int LandmarkCount { get; }

    /// <summary>
    /// Gets the landmark model type used.
    /// </summary>
    public LandmarkModelType ModelType { get; }

    /// <summary>
    /// Initializes a new instance of the landmark extraction step.
    /// </summary>
    public LandmarkExtractionStep(
        ILandmarkExtractor landmarkExtractor,
        ILogger<LandmarkExtractionStep> logger,
        int landmarkCount = 68,
        LandmarkModelType modelType = LandmarkModelType.iBug68Point)
    {
        _landmarkExtractor = landmarkExtractor;
        _logger = logger;
        LandmarkCount = landmarkCount;
        ModelType = modelType;
    }

    /// <summary>
    /// Executes landmark extraction on the processed biometric data.
    /// </summary>
    public async Task<Result<ProcessedBiometricData>> ExecuteAsync(
        ProcessedBiometricData input, 
        IPipelineContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting landmark extraction for execution {ExecutionId}", context.ExecutionId);

        if (!CanProcess(input))
        {
            var error = "Cannot process biometric data without detected faces for landmark extraction";
            _logger.LogWarning(error);
            return Result.Failure<ProcessedBiometricData>(error);
        }

        try
        {
            // Convert biometric data to ImageSharp format for landmark extraction
            using var image = Image.Load<Rgba32>(input.Data.RawData.ToArray());
            
            // Extract landmarks for the primary detected face
            var primaryFace = input.DetectedFaces.OrderByDescending(f => f.Confidence).First();
            
            var landmarkResult = await _landmarkExtractor.ExtractLandmarksAsync(
                image, 
                primaryFace.BoundingBox, 
                cancellationToken);
            
            if (landmarkResult.IsFailure)
            {
                _logger.LogError("Landmark extraction failed: {Error}", landmarkResult.Error);
                return Result.Failure<ProcessedBiometricData>(landmarkResult.Error);
            }

            var landmarks = landmarkResult.Value;
            
            _logger.LogInformation(
                "Landmark extraction completed: {LandmarkCount} points extracted using {ModelType}",
                LandmarkCount, ModelType);

            // Validate landmark quality (basic sanity checks)
            var landmarkQuality = ValidateLandmarkQuality(landmarks);
            
            // Update processed data with landmarks
            var processedData = input
                .WithLandmarks(landmarks)
                .WithProcessingMetadata("landmark_step", StepId)
                .WithProcessingMetadata("landmark_count", LandmarkCount)
                .WithProcessingMetadata("landmark_model", ModelType.ToString())
                .WithProcessingMetadata("landmark_quality", landmarkQuality.Value);

            // Add landmark-based quality metrics if they're reasonable
            if (landmarkQuality.Value >= 0.5f)
            {
                var enhancedQuality = processedData.Quality
                    .WithAdditionalMetric("landmark_quality", landmarkQuality)
                    .WithGeometry(landmarkQuality);

                processedData = processedData with { Quality = enhancedQuality };
            }

            _logger.LogDebug("Landmark extraction step completed successfully");
            return Result.Success(processedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Landmark extraction step failed with exception");
            return Result.Failure<ProcessedBiometricData>($"Landmark extraction failed: {ex.Message}");
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

    /// <summary>
    /// Validates the quality of extracted landmarks with basic sanity checks.
    /// </summary>
    private static QualityScore ValidateLandmarkQuality(FaceLandmarks68 landmarks)
    {
        try
        {
            // Basic validation: check if landmarks form reasonable facial structure
            var points = landmarks.Points;
            
            if (points.Count < 68)
                return QualityScore.Create(0.0f).Value;

            // Check if eye landmarks are positioned reasonably
            var leftEye = points.Skip(36).Take(6).ToList(); // Left eye (36-41)
            var rightEye = points.Skip(42).Take(6).ToList(); // Right eye (42-47)
            
            if (leftEye.Count < 6 || rightEye.Count < 6)
                return QualityScore.Create(0.3f).Value;

            // Calculate eye center positions
            var leftEyeCenter = new Point2D(
                leftEye.Average(p => p.X),
                leftEye.Average(p => p.Y)
            );
            var rightEyeCenter = new Point2D(
                rightEye.Average(p => p.X),
                rightEye.Average(p => p.Y)
            );

            // Check reasonable eye distance (should be positive and reasonable)
            var eyeDistance = Math.Sqrt(
                Math.Pow(rightEyeCenter.X - leftEyeCenter.X, 2) + 
                Math.Pow(rightEyeCenter.Y - leftEyeCenter.Y, 2)
            );

            if (eyeDistance < 20 || eyeDistance > 200)
                return QualityScore.Create(0.4f).Value;

            // Check if nose tip (point 30) is between eyes vertically
            var noseTip = points[30];
            var avgEyeY = (leftEyeCenter.Y + rightEyeCenter.Y) / 2;
            
            if (noseTip.Y < avgEyeY - 50 || noseTip.Y > avgEyeY + 100)
                return QualityScore.Create(0.5f).Value;

            // All basic checks passed
            return QualityScore.Create(0.85f).Value;
        }
        catch
        {
            // If any validation fails, return low quality score
            return QualityScore.Create(0.2f).Value;
        }
    }
}