using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Common;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Infrastructure.Services;
using static OpenPhysical.Biometrics.Core.Domain.Pipeline.BiometricPipelineSteps;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline.Steps;

/// <summary>
/// Pipeline step that encodes biometric images to JPEG 2000 format.
/// </summary>
[PublicAPI]
public sealed class EncodingStep : IJpeg2000EncodingStep
{
    private readonly IJpeg2000Service _jpeg2000Service;
    private readonly ILogger<EncodingStep> _logger;
    private readonly Jpeg2000Options _defaultOptions;
    private readonly Maybe<int> _targetSizeBytes;

    /// <inheritdoc />
    public string StepId => "encoding";

    /// <inheritdoc />
    public string Name => "JPEG 2000 Encoding";

    /// <inheritdoc />
    public string Description =>
        "Encodes biometric image data to JPEG 2000 format with optional ROI";

    /// <inheritdoc />
    public float CompressionRatio => _defaultOptions.CompressionRate;

    /// <inheritdoc />
    public bool RoiEnabled { get; }

    /// <inheritdoc />
    public Maybe<int> TargetSizeBytes => _targetSizeBytes;

    /// <summary>
    /// Gets the encoding options.
    /// </summary>
    public Jpeg2000Options Options => _defaultOptions;

    /// <summary>
    /// Initializes a new instance of the encoding step.
    /// </summary>
    public EncodingStep(
        IJpeg2000Service jpeg2000Service,
        ILogger<EncodingStep> logger,
        Jpeg2000Options? options = null,
        bool roiEnabled = true,
        Maybe<int> targetSizeBytes = default
    )
    {
        _jpeg2000Service = jpeg2000Service;
        _logger = logger;
        _defaultOptions = options ?? new Jpeg2000Options();
        RoiEnabled = roiEnabled;
        _targetSizeBytes = targetSizeBytes;
    }

    /// <inheritdoc />
    public async Task<Result<BiometricData>> ExecuteAsync(
        ProcessedBiometricData input,
        IPipelineContext context,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(new EventId(860868, "EncodingStep"), "EncodingStep stage completed.");

        try
        {
            // Get encoding options from context or use defaults
            var options = GetEncodingOptions(context);

            // Check for ROI set in processing metadata
            Result<EncodedJpeg2000> encodingResult;

            if (
                input.ProcessingMetadata.TryGetValue("roi_set", out var roiData)
                && roiData is FacialRoiSet roiSet
            )
            {
                _logger.LogDebug(
                    new EventId(831474, "EncodingStep"),
                    "EncodingStep stage completed."
                );
                encodingResult = await _jpeg2000Service.EncodeWithRoiAsync(
                    input.Data.RawData,
                    roiSet,
                    options,
                    cancellationToken
                );
            }
            else
            {
                _logger.LogDebug("Encoding without ROI");
                encodingResult = await _jpeg2000Service.EncodeAsync(
                    input.Data.RawData,
                    options,
                    cancellationToken
                );
            }

            if (encodingResult.IsFailure)
            {
                _logger.LogError(new EventId(854526, "EncodingStep"), "EncodingStep failed.");
                return Result.Failure<BiometricData>(encodingResult.Error);
            }

            var encoded = encodingResult.Value;

            // Create output biometric data
            var outputData = new BiometricData(
                encoded.ImageData,
                BiometricDataType.FacialImage,
                input.Data.Format,
                input
                    .Data.Metadata.SetItem("encoding", "JPEG2000")
                    .SetItem("compression_rate", encoded.CompressionRate)
                    .SetItem("file_size", encoded.FileSize)
                    .SetItem("encoded_dimensions", encoded.Dimensions)
            );

            // Store encoded data in processing metadata for easy access
            var updatedMetadata = input
                .ProcessingMetadata.SetItem("encoded_data", encoded.ImageData.ToArray())
                .SetItem("encoding_step", StepId)
                .SetItem("encoding_metadata", encoded.Metadata);

            _logger.LogInformation(
                new EventId(809789, "EncodingStep"),
                "EncodingStep stage completed."
            );

            return Result.Success(outputData);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(706123, "EncodingStep"), "EncodingStep failed.");
            return Result.Failure<BiometricData>($"Encoding failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public bool CanProcess(ProcessedBiometricData input)
    {
        // Can process any facial image data
        return input.Data.DataType == BiometricDataType.FacialImage;
    }

    private Jpeg2000Options GetEncodingOptions(IPipelineContext context)
    {
        var options = _defaultOptions;

        // Override with context values if available
        if (
            context.Metadata.TryGetValue("target_size", out var targetSize)
            && targetSize is int size
            && size > 0
        )
        {
            // Target size mode - will be handled by the service
            _logger.LogDebug("Using target size mode: {TargetSize} bytes", size);
        }
        else if (
            context.Metadata.TryGetValue("compression_rate", out var rate)
            && rate is float compressionRate
        )
        {
            options = options with { CompressionRate = compressionRate };
        }

        if (context.Metadata.TryGetValue("roi_start_level", out var level) && level is int roiLevel)
        {
            options = options with { RoiStartLevel = roiLevel };
        }

        return options;
    }
}
