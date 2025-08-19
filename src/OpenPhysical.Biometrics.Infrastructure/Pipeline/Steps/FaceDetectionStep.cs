using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Immutable;
using static OpenPhysical.Biometrics.Core.Domain.Pipeline.BiometricPipelineSteps;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline.Steps;

/// <summary>
/// Pipeline step that detects faces in biometric image data using RetinaFace model.
/// </summary>
[PublicAPI]
public sealed class FaceDetectionStep : IFaceDetectionStep
{
    private readonly IFaceDetector _faceDetector;
    private readonly ILogger<FaceDetectionStep> _logger;

    /// <summary>
    /// Gets the unique identifier for this pipeline step.
    /// </summary>
    public string StepId => "face-detection";

    /// <summary>
    /// Gets the human-readable name of this step.
    /// </summary>
    public string Name => "Face Detection";

    /// <summary>
    /// Gets the description of what this step does.
    /// </summary>
    public string Description => "Detects faces in biometric images using RetinaFace deep learning model";

    /// <summary>
    /// Gets the minimum confidence threshold for face detection.
    /// </summary>
    public float MinConfidenceThreshold { get; }

    /// <summary>
    /// Gets whether multiple faces are allowed or if only single face detection is performed.
    /// </summary>
    public bool AllowMultipleFaces { get; }

    /// <summary>
    /// Initializes a new instance of the face detection step.
    /// </summary>
    public FaceDetectionStep(
        IFaceDetector faceDetector, 
        ILogger<FaceDetectionStep> logger,
        float minConfidenceThreshold = 0.8f,
        bool allowMultipleFaces = false)
    {
        _faceDetector = faceDetector;
        _logger = logger;
        MinConfidenceThreshold = minConfidenceThreshold;
        AllowMultipleFaces = allowMultipleFaces;
    }

    /// <summary>
    /// Executes face detection on the input biometric data.
    /// </summary>
    public async Task<Result<ProcessedBiometricData>> ExecuteAsync(
        BiometricData input, 
        IPipelineContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting face detection for execution {ExecutionId}", context.ExecutionId);

        if (!CanProcess(input))
        {
            var error = $"Cannot process biometric data of type {input.DataType} with format {input.Format.FormatName}";
            _logger.LogWarning(error);
            return Result.Failure<ProcessedBiometricData>(error);
        }

        try
        {
            // Convert biometric data to ImageSharp format for face detection
            using var image = Image.Load<Rgba32>(input.RawData.ToArray());
            
            // Perform face detection
            var detectionResult = await _faceDetector.DetectFacesAsync(image, cancellationToken);
            
            if (detectionResult.IsFailure)
            {
                _logger.LogError("Face detection failed: {Error}", detectionResult.Error);
                return Result.Failure<ProcessedBiometricData>(detectionResult.Error);
            }

            var detectedFaces = detectionResult.Value;
            
            // Filter faces by confidence threshold
            var qualifyingFaces = detectedFaces
                .Where(face => face.Confidence >= MinConfidenceThreshold)
                .ToImmutableList();

            _logger.LogInformation(
                "Face detection completed: {TotalFaces} detected, {QualifyingFaces} above threshold {Threshold}",
                detectedFaces.Count, qualifyingFaces.Count, MinConfidenceThreshold);

            // Validate face count constraints
            if (!AllowMultipleFaces && qualifyingFaces.Count > 1)
            {
                var error = $"Multiple faces detected ({qualifyingFaces.Count}) but only single face allowed";
                _logger.LogWarning(error);
                return Result.Failure<ProcessedBiometricData>(error);
            }

            if (qualifyingFaces.Count == 0)
            {
                var error = $"No faces detected with confidence >= {MinConfidenceThreshold}";
                _logger.LogWarning(error);
                return Result.Failure<ProcessedBiometricData>(error);
            }

            // Create initial quality metrics
            var qualityMetrics = QualityMetrics.WithOverallQuality(
                QualityScore.Create(qualifyingFaces.Max(f => f.Confidence)).Value
            );

            // Create processed biometric data
            var processedData = ProcessedBiometricData
                .FromBiometricData(input, qualityMetrics)
                .WithDetectedFaces(qualifyingFaces)
                .WithProcessingMetadata("step", StepId)
                .WithProcessingMetadata("faces_detected", detectedFaces.Count)
                .WithProcessingMetadata("faces_qualifying", qualifyingFaces.Count)
                .WithProcessingMetadata("confidence_threshold", MinConfidenceThreshold);

            _logger.LogDebug("Face detection step completed successfully");
            return Result.Success(processedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Face detection step failed with exception");
            return Result.Failure<ProcessedBiometricData>($"Face detection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines if this step can process the given input.
    /// </summary>
    public bool CanProcess(BiometricData input)
    {
        // Can process facial images in standard image formats
        return input.DataType == BiometricDataType.FacialImage && 
               (input.Format == BiometricFormat.Jpeg || 
                input.Format == BiometricFormat.Jpeg2000 || 
                input.Format == BiometricFormat.Png);
    }
}