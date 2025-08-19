using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Common;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Immutable;
using static OpenPhysical.Biometrics.Core.Domain.Pipeline.BiometricPipelineSteps;
using DomainImageDimensions = OpenPhysical.Domains.Biometrics.ImageDimensions;
using CoreImageDimensions = OpenPhysical.Biometrics.Core.Domain.Transformations.ImageDimensions;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline.Steps;

/// <summary>
/// Pipeline step that transforms images to meet standard requirements.
/// </summary>
[PublicAPI]
public sealed class ImageTransformationStep : IImageTransformStep
{
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<ImageTransformationStep> _logger;
    private readonly CoreImageDimensions _targetDimensions;
    private readonly TransformationOptions _options;

    /// <inheritdoc />
    public string StepId => "image-transformation";

    /// <inheritdoc />
    public string Name => "Image Transformation";

    /// <inheritdoc />
    public string Description => "Transforms image to meet standard dimensions and requirements";

    /// <inheritdoc />
    public DomainImageDimensions TargetDimensions => 
        DomainImageDimensions.Create(_targetDimensions.Width, _targetDimensions.Height).Value;

    /// <inheritdoc />
    public float MaxRotationDegrees { get; }

    /// <summary>
    /// Initializes a new instance of the image transformation step.
    /// </summary>
    public ImageTransformationStep(
        IImageProcessingService imageProcessingService,
        ILogger<ImageTransformationStep> logger,
        CoreImageDimensions? targetDimensions = null,
        TransformationOptions? options = null,
        float maxRotationDegrees = 15.0f)
    {
        _imageProcessingService = imageProcessingService;
        _logger = logger;
        _targetDimensions = targetDimensions ?? new CoreImageDimensions(300, 400); // PIV default
        _options = options ?? new TransformationOptions();
        MaxRotationDegrees = maxRotationDegrees;
    }

    /// <inheritdoc />
    public async Task<Result<ProcessedBiometricData>> ExecuteAsync(
        ProcessedBiometricData input, 
        IPipelineContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting image transformation for execution {ExecutionId}", context.ExecutionId);

        try
        {
            // Get target dimensions from context or use default
            var targetDims = context.Metadata.TryGetValue("target_dimensions", out var dims) && dims is CoreImageDimensions dimensions
                ? dimensions
                : _targetDimensions;

            // Transform the image
            var result = await _imageProcessingService.TransformToStandardAsync(
                input.Data.RawData,
                targetDims,
                _options,
                cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogError("Image transformation failed: {Error}", result.Error);
                return Result.Failure<ProcessedBiometricData>(result.Error);
            }

            var transformed = result.Value;

            // Update biometric data with transformed image
            var transformedData = new BiometricData(
                transformed.ImageData,
                input.Data.DataType,
                input.Data.Format,
                input.Data.Metadata
                    .SetItem("transformed", true)
                    .SetItem("original_dimensions", $"{input.Data.Metadata.GetValueOrDefault("width")}x{input.Data.Metadata.GetValueOrDefault("height")}")
                    .SetItem("target_dimensions", $"{transformed.Dimensions.Width}x{transformed.Dimensions.Height}"));

            // Create ROI set based on transformed dimensions
            var roiSet = FacialRoiSet.CalculateRoiForDimensions(
                transformed.Dimensions.Width, 
                transformed.Dimensions.Height);

            var processedData = input with
            {
                Data = transformedData,
                ProcessingMetadata = input.ProcessingMetadata
                    .SetItem("transformation_step", StepId)
                    .SetItem("transformed_dimensions", transformed.Dimensions)
                    .SetItem("transformation_type", transformed.Type.ToString())
                    .SetItem("roi_set", roiSet)
            };

            _logger.LogInformation(
                "Image transformed from original to {Width}x{Height}",
                transformed.Dimensions.Width, transformed.Dimensions.Height);

            return Result.Success(processedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image transformation step failed with exception");
            return Result.Failure<ProcessedBiometricData>($"Image transformation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public bool CanProcess(ProcessedBiometricData input)
    {
        // Can process any biometric data with image type
        return input.Data.DataType == BiometricDataType.FacialImage;
    }
}