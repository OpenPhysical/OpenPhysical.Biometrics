using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

/// <summary>
/// Extension methods for integrating quality assessment into the processing pipeline
/// </summary>
[PublicAPI]
public static class QualityExtensions
{
    /// <summary>
    /// Processes an image with quality assessment
    /// </summary>
    public static async Task<Result<(ProcessingResultDto Result, Iso19794Assessment Quality)>> 
        ProcessWithQualityAsync(
            this byte[] imageData,
            ProcessingOptions processingOptions,
            QualityAssessmentOptions qualityOptions,
            string format = "jp2",
            int jpegQuality = 85,
            ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        
        using var services = BiometricProcessingServices.CreateDefault(logger);
        
        // Step 1: Detect face
        using var image = Image.Load<Rgba32>(imageData);
        var faceResult = await services.FaceDetector.DetectFacesAsync(image);
        if (faceResult.IsFailure)
        {
            return Result.Failure<(ProcessingResultDto, Iso19794Assessment)>(
                $"Face detection failed: {faceResult.Error}");
        }
        
        var faces = faceResult.Value;
        if (!faces.Any())
        {
            return Result.Failure<(ProcessingResultDto, Iso19794Assessment)>(
                "No faces detected in the image");
        }
        
        var primaryFace = faces.OrderByDescending(f => f.Confidence).First();
        
        // Step 2: Extract landmarks
        var landmarksResult = await services.LandmarkExtractor
            .ExtractLandmarksAsync(image, primaryFace.BoundingBox);
        if (landmarksResult.IsFailure)
        {
            return Result.Failure<(ProcessingResultDto, Iso19794Assessment)>(
                $"Landmark extraction failed: {landmarksResult.Error}");
        }
        
        // Step 3: Perform quality assessment
        var qualityResult = await services.QualityAssessor.AssessAsync(
            image,
            primaryFace,
            landmarksResult.Value,
            qualityOptions);
        
        if (qualityResult.IsFailure)
        {
            return Result.Failure<(ProcessingResultDto, Iso19794Assessment)>(
                $"Quality assessment failed: {qualityResult.Error}");
        }
        
        var quality = qualityResult.Value;
        
        // Step 4: Check if quality meets threshold
        if (qualityOptions.EnforceCompliance && !quality.IsCompliant)
        {
            return Result.Failure<(ProcessingResultDto, Iso19794Assessment)>(
                $"Image quality {quality.Overall} does not meet compliance requirements. " +
                $"Critical violations: {quality.Violations.Count(v => v.Severity == ViolationSeverity.Critical)}");
        }
        
        if (quality.Overall.Value < qualityOptions.MinQualityThreshold)
        {
            return Result.Failure<(ProcessingResultDto, Iso19794Assessment)>(
                $"Image quality {quality.Overall} is below minimum threshold {qualityOptions.MinQualityThreshold:P0}");
        }
        
        // Step 5: Process the image using functional service
        // Create a typed logger for the encoder service
        var loggerFactory = logger.GetType().GetProperty("Factory")?.GetValue(logger) as ILoggerFactory;
        var encoderLogger = loggerFactory?.CreateLogger<FacialImageEncoderService>() 
            ?? NullLogger<FacialImageEncoderService>.Instance;
        
        var facialImageEncoderService = new FacialImageEncoderService(
            services.FaceDetector,
            services.LandmarkExtractor,
            services.Jpeg2000Encoder,
            encoderLogger);
        
        var processingResult = await facialImageEncoderService.ProcessAsync(
            imageData,
            processingOptions,
            format,
            jpegQuality);
        
        if (processingResult.IsFailure)
        {
            return Result.Failure<(ProcessingResultDto, Iso19794Assessment)>(
                $"Image processing failed: {processingResult.Error}");
        }
        
        return Result.Success((processingResult.Value, quality));
    }
    
    /// <summary>
    /// Performs quality assessment on an image without processing
    /// </summary>
    public static async Task<Result<Iso19794Assessment>> AssessQualityAsync(
        this byte[] imageData,
        QualityAssessmentOptions? options = null,
        ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        options ??= new QualityAssessmentOptions();
        
        using var services = BiometricProcessingServices.CreateDefault(logger);
        using var image = Image.Load<Rgba32>(imageData);
        
        // Detect face
        var faceResult = await services.FaceDetector.DetectFacesAsync(image);
        if (faceResult.IsFailure)
        {
            return Result.Failure<Iso19794Assessment>($"Face detection failed: {faceResult.Error}");
        }
        
        var faces = faceResult.Value;
        if (!faces.Any())
        {
            return Result.Failure<Iso19794Assessment>("No faces detected in the image");
        }
        
        var primaryFace = faces.OrderByDescending(f => f.Confidence).First();
        
        // Extract landmarks
        var landmarksResult = await services.LandmarkExtractor
            .ExtractLandmarksAsync(image, primaryFace.BoundingBox);
        if (landmarksResult.IsFailure)
        {
            return Result.Failure<Iso19794Assessment>($"Landmark extraction failed: {landmarksResult.Error}");
        }
        
        // Perform quality assessment
        return await services.QualityAssessor.AssessAsync(
            image,
            primaryFace,
            landmarksResult.Value,
            options);
    }
    
    /// <summary>
    /// Validates compliance with a specific standard without processing
    /// </summary>
    public static async Task<Result<ComplianceAssessment>> ValidateComplianceAsync(
        this byte[] imageData,
        string standardName = "PIV",
        float minConfidence = 0.8f,
        ILogger? logger = null,
        AssessmentMode mode = AssessmentMode.InputValidation)
    {
        logger ??= NullLogger.Instance;
        
        using var services = BiometricProcessingServices.CreateDefault(logger);
        using var image = Image.Load<Rgba32>(imageData);
        
        // Detect face
        var faceResult = await services.FaceDetector.DetectFacesAsync(image);
        if (faceResult.IsFailure)
        {
            return Result.Failure<ComplianceAssessment>($"Face detection failed: {faceResult.Error}");
        }
        
        var faces = faceResult.Value;
        if (!faces.Any())
        {
            return Result.Failure<ComplianceAssessment>("No faces detected in the image");
        }
        
        var primaryFace = faces.OrderByDescending(f => f.Confidence).First();
        if (primaryFace.Confidence < minConfidence)
        {
            return Result.Failure<ComplianceAssessment>(
                $"Face detection confidence {primaryFace.Confidence:P1} is below minimum {minConfidence:P1}");
        }
        
        // Extract landmarks
        var landmarksResult = await services.LandmarkExtractor
            .ExtractLandmarksAsync(image, primaryFace.BoundingBox);
        if (landmarksResult.IsFailure)
        {
            return Result.Failure<ComplianceAssessment>($"Landmark extraction failed: {landmarksResult.Error}");
        }
        
        // Perform compliance assessment using new pipeline
        return await ComplianceAssessmentPipeline.AssessComplianceAsync(
            image,
            primaryFace,
            landmarksResult.Value,
            standardName,
            mode);
    }
}