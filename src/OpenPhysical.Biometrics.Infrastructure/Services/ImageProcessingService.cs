using System.Collections.Immutable;
using System.Drawing;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Common;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// Service for image processing and transformation operations.
/// Provides library-first functionality for biometric image processing.
/// </summary>
[PublicAPI]
public interface IImageProcessingService
{
    /// <summary>
    /// Transforms an image to comply with specified standard dimensions and requirements.
    /// </summary>
    Task<Result<TransformedImage>> TransformToStandardAsync(
        ImmutableArray<byte> imageData,
        ImageDimensions targetDimensions,
        TransformationOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Crops an image to focus on detected biometric features.
    /// </summary>
    Task<Result<CroppedImage>> CropToBiometricAsync(
        ImmutableArray<byte> imageData,
        DetectedFace face,
        CropOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Rotates an image to align biometric features properly.
    /// </summary>
    Task<Result<RotatedImage>> RotateForAlignmentAsync(
        ImmutableArray<byte> imageData,
        float rotationDegrees,
        RotationOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Resizes an image while maintaining aspect ratio.
    /// </summary>
    Task<Result<ResizedImage>> ResizeWithAspectRatioAsync(
        ImmutableArray<byte> imageData,
        ImageDimensions targetDimensions,
        ResizeOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Applies a series of transformations in sequence.
    /// </summary>
    Task<Result<TransformedImage>> ApplyTransformationPipelineAsync(
        ImmutableArray<byte> imageData,
        ImmutableList<IImageTransformation> transformations,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Implementation of image processing service.
/// </summary>
[PublicAPI]
public sealed class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;

    /// <summary>
    /// Initializes a new instance of the image processing service.
    /// </summary>
    public ImageProcessingService(ILogger<ImageProcessingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<TransformedImage>> TransformToStandardAsync(
        ImmutableArray<byte> imageData,
        ImageDimensions targetDimensions,
        TransformationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                new EventId(279000, "ImageProcessingService"),
                "ImageProcessingService processing started."
            );

            using var image = RgbImage.Load(imageData.ToArray());
            var originalDimensions = new ImageDimensions(image.Width, image.Height);

            // Calculate scaling to fit target dimensions
            var scale = CalculateScaleToFit(
                originalDimensions,
                targetDimensions,
                options.MaintainAspectRatio
            );

            // Apply transformations
            image.Mutate(x =>
            {
                if (options.MaintainAspectRatio)
                {
                    x.Resize(new Size((int)(image.Width * scale), (int)(image.Height * scale)));

                    // Center crop if needed
                    if (
                        image.Width != targetDimensions.Width
                        || image.Height != targetDimensions.Height
                    )
                    {
                        var cropRect = CalculateCenterCrop(
                            new ImageDimensions(image.Width, image.Height),
                            targetDimensions
                        );
                        x.Crop(cropRect);
                    }
                }
                else
                {
                    x.Resize(new Size(targetDimensions.Width, targetDimensions.Height));
                }

                // Apply background color if specified
                if (options.BackgroundColor.HasValue)
                {
                    x.BackgroundColor(options.BackgroundColor.Value);
                }
            });

            // Convert to bytes
            using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms, cancellationToken);

            var transformedData = ImmutableArray.Create(ms.ToArray());
            var metadata = ImmutableDictionary<string, object>
                .Empty.Add("OriginalDimensions", originalDimensions)
                .Add("TargetDimensions", targetDimensions)
                .Add("Scale", scale)
                .Add("AspectRatioMaintained", options.MaintainAspectRatio);

            var result = new TransformedImage(
                transformedData,
                targetDimensions,
                TransformationType.StandardCompliance,
                metadata
            );

            _logger.LogDebug(
                new EventId(279001, "ImageProcessingService"),
                "Standard transformation completed successfully"
            );
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(279002, "ImageProcessingService"),
                "Standard transformation failed"
            );
            return Result.Failure<TransformedImage>($"Transformation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<CroppedImage>> CropToBiometricAsync(
        ImmutableArray<byte> imageData,
        DetectedFace face,
        CropOptions options,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                new EventId(279003, "ImageProcessingService"),
                "ImageProcessingService processing started."
            );

            using var image = RgbImage.Load(imageData.ToArray());

            // Calculate crop region with padding
            var cropRect = CalculateBiometricCrop(
                face.BoundingBox,
                new ImageDimensions(image.Width, image.Height),
                options
            );

            // Apply crop
            image.Mutate(x => x.Crop(cropRect));

            // Convert to bytes
            using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms, cancellationToken);

            var croppedData = ImmutableArray.Create(ms.ToArray());
            var croppedDimensions = new ImageDimensions(cropRect.Width, cropRect.Height);

            var result = new CroppedImage(
                croppedData,
                croppedDimensions,
                face.BoundingBox,
                cropRect
            );

            _logger.LogDebug(
                new EventId(279004, "ImageProcessingService"),
                "Biometric crop completed successfully"
            );
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(279005, "ImageProcessingService"),
                "Biometric crop failed"
            );
            return Result.Failure<CroppedImage>($"Crop failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<RotatedImage>> RotateForAlignmentAsync(
        ImmutableArray<byte> imageData,
        float rotationDegrees,
        RotationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                new EventId(279006, "ImageProcessingService"),
                "ImageProcessingService processing started."
            );

            using var image = RgbImage.Load(imageData.ToArray());

            var originalSize = new Size(image.Width, image.Height);
            image.Rotate(rotationDegrees);
            if (options.CropToOriginalSize)
            {
                // A quarter turn can make one side narrower than the original canvas.
                image.Pad(
                    Math.Max(image.Width, originalSize.Width),
                    Math.Max(image.Height, originalSize.Height)
                );
                image.Crop(
                    new Rectangle(
                        (image.Width - originalSize.Width) / 2,
                        (image.Height - originalSize.Height) / 2,
                        originalSize.Width,
                        originalSize.Height
                    )
                );
            }

            // Convert to bytes
            using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms, cancellationToken);

            var rotatedData = ImmutableArray.Create(ms.ToArray());
            var rotatedDimensions = new ImageDimensions(image.Width, image.Height);

            var result = new RotatedImage(rotatedData, rotatedDimensions, rotationDegrees);

            _logger.LogDebug(
                new EventId(279007, "ImageProcessingService"),
                "Rotation completed successfully"
            );
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(279008, "ImageProcessingService"), "Rotation failed");
            return Result.Failure<RotatedImage>($"Rotation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ResizedImage>> ResizeWithAspectRatioAsync(
        ImmutableArray<byte> imageData,
        ImageDimensions targetDimensions,
        ResizeOptions options,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                new EventId(279009, "ImageProcessingService"),
                "ImageProcessingService processing started."
            );

            using var image = RgbImage.Load(imageData.ToArray());
            var originalDimensions = new ImageDimensions(image.Width, image.Height);

            // Calculate new dimensions maintaining aspect ratio
            var (newWidth, newHeight) = CalculateAspectRatioSize(
                originalDimensions,
                targetDimensions,
                options.ResizeMode
            );

            // Apply resize
            image.Mutate(x =>
            {
                x.Resize(new Size(newWidth, newHeight));

                // Apply padding if needed
                if (
                    options.PadToTarget
                    && (newWidth < targetDimensions.Width || newHeight < targetDimensions.Height)
                )
                {
                    var padLeft = (targetDimensions.Width - newWidth) / 2;
                    var padTop = (targetDimensions.Height - newHeight) / 2;

                    x.Pad(targetDimensions.Width, targetDimensions.Height);

                    if (options.PaddingColor.HasValue)
                    {
                        x.BackgroundColor(options.PaddingColor.Value);
                    }
                }
            });

            // Convert to bytes
            using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms, cancellationToken);

            var resizedData = ImmutableArray.Create(ms.ToArray());
            var finalDimensions = new ImageDimensions(image.Width, image.Height);
            var scale = (float)newWidth / originalDimensions.Width;

            var result = new ResizedImage(resizedData, finalDimensions, originalDimensions, scale);

            _logger.LogDebug(
                new EventId(279010, "ImageProcessingService"),
                "Resize completed successfully"
            );
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(279011, "ImageProcessingService"), "Resize failed");
            return Result.Failure<ResizedImage>($"Resize failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<TransformedImage>> ApplyTransformationPipelineAsync(
        ImmutableArray<byte> imageData,
        ImmutableList<IImageTransformation> transformations,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                new EventId(279012, "ImageProcessingService"),
                "ImageProcessingService processing started."
            );

            var currentData = imageData;
            var metadata = ImmutableDictionary<string, object>.Empty;
            ImageDimensions currentDimensions;

            using (var image = RgbImage.Load(currentData.ToArray()))
            {
                currentDimensions = new ImageDimensions(image.Width, image.Height);
            }

            foreach (var transformation in transformations)
            {
                _logger.LogDebug(
                    new EventId(279013, "ImageProcessingService"),
                    "ImageProcessingService processing status changed."
                );

                var result = await transformation.ApplyAsync(currentData, cancellationToken);
                if (result.IsFailure)
                {
                    return Result.Failure<TransformedImage>(
                        $"Pipeline failed at {transformation.GetType().Name}: {result.Error}"
                    );
                }

                currentData = result.Value.ImageData;
                currentDimensions = result.Value.Dimensions;

                // Merge metadata
                foreach (var kvp in result.Value.Metadata)
                {
                    metadata = metadata.SetItem(
                        $"{transformation.GetType().Name}.{kvp.Key}",
                        kvp.Value
                    );
                }
            }

            var finalResult = new TransformedImage(
                currentData,
                currentDimensions,
                TransformationType.Pipeline,
                metadata.Add("TransformationCount", transformations.Count)
            );

            _logger.LogDebug(
                new EventId(279014, "ImageProcessingService"),
                "Transformation pipeline completed successfully"
            );
            return Result.Success(finalResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(279015, "ImageProcessingService"),
                "Transformation pipeline failed"
            );
            return Result.Failure<TransformedImage>($"Pipeline failed: {ex.Message}");
        }
    }

    private static float CalculateScaleToFit(
        ImageDimensions source,
        ImageDimensions target,
        bool maintainAspectRatio
    )
    {
        if (!maintainAspectRatio)
        {
            return 1.0f; // Will be stretched to fit
        }

        var scaleX = (float)target.Width / source.Width;
        var scaleY = (float)target.Height / source.Height;

        // Use larger scale to ensure image fills target (will crop excess)
        return Math.Max(scaleX, scaleY);
    }

    private static Rectangle CalculateCenterCrop(ImageDimensions source, ImageDimensions target)
    {
        var x = (source.Width - target.Width) / 2;
        var y = (source.Height - target.Height) / 2;

        return new Rectangle(
            Math.Max(0, x),
            Math.Max(0, y),
            Math.Min(target.Width, source.Width),
            Math.Min(target.Height, source.Height)
        );
    }

    private static Rectangle CalculateBiometricCrop(
        FaceBox face,
        ImageDimensions imageDimensions,
        CropOptions options
    )
    {
        // Apply padding
        var paddingX = (int)(face.Width * options.PaddingFactor);
        var paddingY = (int)(face.Height * options.PaddingFactor);

        var x = Math.Max(0, face.X - paddingX);
        var y = Math.Max(0, face.Y - paddingY);
        var width = Math.Min(imageDimensions.Width - x, face.Width + 2 * paddingX);
        var height = Math.Min(imageDimensions.Height - y, face.Height + 2 * paddingY);

        // Ensure minimum size
        if (options.MinimumSize.HasValue)
        {
            width = Math.Max(width, options.MinimumSize.Value.Width);
            height = Math.Max(height, options.MinimumSize.Value.Height);
        }

        return new Rectangle((int)x, (int)y, (int)width, (int)height);
    }

    private static (int Width, int Height) CalculateAspectRatioSize(
        ImageDimensions source,
        ImageDimensions target,
        ResizeMode mode
    )
    {
        var aspectRatio = (float)source.Width / source.Height;

        return mode switch
        {
            ResizeMode.Fit => CalculateFitSize(source, target),
            ResizeMode.Fill => CalculateFillSize(source, target),
            ResizeMode.Stretch => (target.Width, target.Height),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    private static (int Width, int Height) CalculateFitSize(
        ImageDimensions source,
        ImageDimensions target
    )
    {
        var scaleX = (float)target.Width / source.Width;
        var scaleY = (float)target.Height / source.Height;
        var scale = Math.Min(scaleX, scaleY);

        return ((int)(source.Width * scale), (int)(source.Height * scale));
    }

    private static (int Width, int Height) CalculateFillSize(
        ImageDimensions source,
        ImageDimensions target
    )
    {
        var scaleX = (float)target.Width / source.Width;
        var scaleY = (float)target.Height / source.Height;
        var scale = Math.Max(scaleX, scaleY);

        return ((int)(source.Width * scale), (int)(source.Height * scale));
    }
}

/// <summary>
/// Options for image transformation operations.
/// </summary>
[PublicAPI]
public sealed record TransformationOptions(
    bool MaintainAspectRatio = true,
    Maybe<Color> BackgroundColor = default
);

/// <summary>
/// Options for image cropping operations.
/// </summary>
[PublicAPI]
public sealed record CropOptions(
    float PaddingFactor = 0.2f,
    Maybe<ImageDimensions> MinimumSize = default
);

/// <summary>
/// Options for image rotation operations.
/// </summary>
[PublicAPI]
public sealed record RotationOptions(bool CropToOriginalSize = true);

/// <summary>
/// Options for image resizing operations.
/// </summary>
[PublicAPI]
public sealed record ResizeOptions(
    ResizeMode ResizeMode = ResizeMode.Fit,
    bool PadToTarget = false,
    Maybe<Color> PaddingColor = default
);

/// <summary>
/// Resize modes for aspect ratio handling.
/// </summary>
[PublicAPI]
public enum ResizeMode
{
    /// <summary>
    /// Fit image within target dimensions (may have padding).
    /// </summary>
    Fit,

    /// <summary>
    /// Fill target dimensions (may crop).
    /// </summary>
    Fill,

    /// <summary>
    /// Stretch to exact dimensions (ignores aspect ratio).
    /// </summary>
    Stretch,
}

/// <summary>
/// Result of an image transformation.
/// </summary>
[PublicAPI]
public sealed record TransformedImage(
    ImmutableArray<byte> ImageData,
    ImageDimensions Dimensions,
    TransformationType Type,
    ImmutableDictionary<string, object> Metadata
);

/// <summary>
/// Result of an image crop operation.
/// </summary>
[PublicAPI]
public sealed record CroppedImage(
    ImmutableArray<byte> ImageData,
    ImageDimensions Dimensions,
    FaceBox OriginalRegion,
    Rectangle CroppedRegion
);

/// <summary>
/// Result of an image rotation operation.
/// </summary>
[PublicAPI]
public sealed record RotatedImage(
    ImmutableArray<byte> ImageData,
    ImageDimensions Dimensions,
    float RotationDegrees
);

/// <summary>
/// Result of an image resize operation.
/// </summary>
[PublicAPI]
public sealed record ResizedImage(
    ImmutableArray<byte> ImageData,
    ImageDimensions Dimensions,
    ImageDimensions OriginalDimensions,
    float Scale
);

/// <summary>
/// Types of image transformations.
/// </summary>
[PublicAPI]
public enum TransformationType
{
    /// <summary>
    /// Transformation to meet standard requirements.
    /// </summary>
    StandardCompliance,

    /// <summary>
    /// Multiple transformations applied in sequence.
    /// </summary>
    Pipeline,

    /// <summary>
    /// Custom transformation.
    /// </summary>
    Custom,
}

/// <summary>
/// Interface for custom image transformations.
/// </summary>
[PublicAPI]
public interface IImageTransformation
{
    /// <summary>
    /// Applies the transformation to the image data.
    /// </summary>
    Task<Result<TransformationResult>> ApplyAsync(
        ImmutableArray<byte> imageData,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Result of a transformation operation.
/// </summary>
[PublicAPI]
public sealed record TransformationResult(
    ImmutableArray<byte> ImageData,
    ImageDimensions Dimensions,
    ImmutableDictionary<string, object> Metadata
);
