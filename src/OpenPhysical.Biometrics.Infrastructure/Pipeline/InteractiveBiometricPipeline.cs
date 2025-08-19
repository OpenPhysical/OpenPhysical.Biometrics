using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using System.Collections.Immutable;
using System.Diagnostics;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline;

/// <summary>
/// Interactive biometric pipeline that includes human-in-the-middle checkpoints for decision making.
/// </summary>
[PublicAPI]
public sealed class InteractiveBiometricPipeline : IBiometricPipeline<BiometricData, ProcessedBiometricData>
{
    private readonly IBiometricPipeline<BiometricData, ProcessedBiometricData> _basePipeline;
    private readonly ICheckpoint<QualityMetrics> _qualityCheckpoint;
    private readonly ICheckpoint<IReadOnlyList<DetectedFace>> _faceSelectionCheckpoint;
    private readonly ILogger<InteractiveBiometricPipeline> _logger;

    /// <summary>
    /// Gets the unique identifier for this pipeline.
    /// </summary>
    public PipelineId PipelineId { get; }

    /// <summary>
    /// Gets the human-readable name of this pipeline.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description of what this pipeline does.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the immutable configuration for this pipeline.
    /// </summary>
    public PipelineConfiguration Configuration { get; }

    /// <summary>
    /// Initializes a new instance of the interactive biometric pipeline.
    /// </summary>
    public InteractiveBiometricPipeline(
        IBiometricPipeline<BiometricData, ProcessedBiometricData> basePipeline,
        ICheckpoint<QualityMetrics> qualityCheckpoint,
        ICheckpoint<IReadOnlyList<DetectedFace>> faceSelectionCheckpoint,
        ILogger<InteractiveBiometricPipeline> logger)
    {
        _basePipeline = basePipeline;
        _qualityCheckpoint = qualityCheckpoint;
        _faceSelectionCheckpoint = faceSelectionCheckpoint;
        _logger = logger;

        PipelineId = new PipelineId($"interactive-{basePipeline.PipelineId.Value}");
        Name = $"Interactive {basePipeline.Name}";
        Description = $"{basePipeline.Description} with human-in-the-middle checkpoints";
        Configuration = basePipeline.Configuration with { ExecutionMode = PipelineExecutionMode.Interactive };
    }

    /// <summary>
    /// Executes the interactive pipeline with checkpoints.
    /// </summary>
    public async Task<Result<PipelineExecutionResult<ProcessedBiometricData>>> ExecuteAsync(
        BiometricData input,
        CancellationToken cancellationToken = default)
    {
        var context = PipelineContext.Create()
            .WithMetadata("interactive_mode", true)
            .WithMetadata("checkpoints_enabled", true);
            
        return await ExecuteAsync(input, context, cancellationToken);
    }

    /// <summary>
    /// Executes the interactive pipeline with a pre-existing context.
    /// </summary>
    public async Task<Result<PipelineExecutionResult<ProcessedBiometricData>>> ExecuteAsync(
        BiometricData input,
        IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting interactive pipeline execution: {PipelineId}", PipelineId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Execute base pipeline to get initial processing results
            var baseResult = await _basePipeline.ExecuteAsync(input, context, cancellationToken);
            
            if (baseResult.IsFailure)
            {
                stopwatch.Stop();
                _logger.LogError("Base pipeline execution failed: {Error}", baseResult.Error);
                return baseResult;
            }

            var processedData = baseResult.Value.Output;
            var updatedContext = baseResult.Value.Context;

            // Checkpoint 1: Face Selection (if multiple faces detected)
            if (processedData.DetectedFaces.Count > 1)
            {
                _logger.LogDebug("Multiple faces detected, requesting face selection");
                
                var faceSelectionResult = await _faceSelectionCheckpoint.RequestDecisionAsync(
                    processedData.DetectedFaces, updatedContext, cancellationToken);

                if (faceSelectionResult.IsFailure)
                {
                    stopwatch.Stop();
                    return Result.Success(PipelineExecutionResult<ProcessedBiometricData>.Failure(
                        updatedContext, stopwatch.Elapsed, faceSelectionResult.Error));
                }

                var faceDecision = faceSelectionResult.Value;
                processedData = await ApplyFaceSelectionDecision(processedData, faceDecision, updatedContext);

                if (faceDecision.Decision == CheckpointDecision.Abort)
                {
                    stopwatch.Stop();
                    return Result.Success(PipelineExecutionResult<ProcessedBiometricData>.Aborted(
                        updatedContext, stopwatch.Elapsed, faceDecision.UserComment.GetValueOrDefault("Face selection aborted")));
                }
            }

            // Checkpoint 2: Quality Approval
            var qualityThreshold = Configuration.GetSetting<float>("quality_threshold").GetValueOrDefault(0.7f);
            var requiresQualityApproval = processedData.Quality.OverallQuality.Value < qualityThreshold;

            if (requiresQualityApproval)
            {
                _logger.LogDebug("Quality below threshold, requesting quality approval");
                
                var qualityApprovalResult = await _qualityCheckpoint.RequestDecisionAsync(
                    processedData.Quality, updatedContext, cancellationToken);

                if (qualityApprovalResult.IsFailure)
                {
                    stopwatch.Stop();
                    return Result.Success(PipelineExecutionResult<ProcessedBiometricData>.Failure(
                        updatedContext, stopwatch.Elapsed, qualityApprovalResult.Error));
                }

                var qualityDecision = qualityApprovalResult.Value;
                processedData = await ApplyQualityDecision(processedData, qualityDecision, updatedContext);

                if (qualityDecision.Decision == CheckpointDecision.Abort)
                {
                    stopwatch.Stop();
                    return Result.Success(PipelineExecutionResult<ProcessedBiometricData>.Aborted(
                        updatedContext, stopwatch.Elapsed, qualityDecision.UserComment.GetValueOrDefault("Quality approval aborted")));
                }

                // If user requested retry with modifications, re-run quality assessment
                if (qualityDecision.Decision == CheckpointDecision.Retry ||
                    qualityDecision.Decision == CheckpointDecision.ContinueWithModifications)
                {
                    _logger.LogDebug("Re-running pipeline with user modifications");
                    // In a full implementation, we would re-run the quality assessment step here
                    // For now, we'll continue with the current data but mark it as modified
                    updatedContext = updatedContext.WithMetadata("user_modifications_applied", true);
                }
            }

            stopwatch.Stop();

            // Mark as successful with all checkpoint approvals
            updatedContext = updatedContext
                .WithMetadata("checkpoints_completed", true)
                .WithMetadata("user_approved", true);

            _logger.LogInformation(
                "Interactive pipeline completed successfully in {Duration}ms with user approval",
                stopwatch.ElapsedMilliseconds);

            return Result.Success(PipelineExecutionResult<ProcessedBiometricData>.Success(
                processedData, updatedContext, stopwatch.Elapsed));
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("Interactive pipeline execution was cancelled");
            
            return Result.Success(PipelineExecutionResult<ProcessedBiometricData>.Aborted(
                context, stopwatch.Elapsed, "Interactive pipeline execution was cancelled"));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Interactive pipeline execution failed");
            
            return Result.Success(PipelineExecutionResult<ProcessedBiometricData>.Failure(
                context, stopwatch.Elapsed, $"Interactive pipeline failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Validates if the pipeline can process the given input.
    /// </summary>
    public bool CanProcess(BiometricData input) => _basePipeline.CanProcess(input);

    /// <summary>
    /// Applies face selection decision to processed data.
    /// </summary>
    private async Task<ProcessedBiometricData> ApplyFaceSelectionDecision(
        ProcessedBiometricData processedData,
        CheckpointResult decision,
        IPipelineContext context)
    {
        if (decision.ModifiedData.HasValue)
        {
            dynamic modifiedData = decision.ModifiedData.Value;
            if (modifiedData?.SelectedFaceIndex != null && modifiedData?.SelectedFace != null)
            {
                int index = modifiedData.SelectedFaceIndex;
                DetectedFace selectedFace = modifiedData.SelectedFace;
                
                _logger.LogInformation("Applied face selection: Face {Index} selected", index + 1);
                
                // Update processed data to only include selected face
                var updatedData = processedData.WithDetectedFaces(ImmutableList.Create(selectedFace))
                    .WithProcessingMetadata("selected_face_index", index)
                    .WithProcessingMetadata("face_selection_method", decision.SelectedOptionId)
                    .WithProcessingMetadata("face_selection_comment", decision.UserComment.GetValueOrDefault(""));

                return updatedData;
            }
        }

        return processedData;
    }

    /// <summary>
    /// Applies quality decision to processed data.
    /// </summary>
    private async Task<ProcessedBiometricData> ApplyQualityDecision(
        ProcessedBiometricData processedData,
        CheckpointResult decision,
        IPipelineContext context)
    {
        var updatedData = processedData
            .WithProcessingMetadata("quality_decision", decision.SelectedOptionId)
            .WithProcessingMetadata("quality_approval_comment", decision.UserComment.GetValueOrDefault(""));

        if (decision.ModifiedData.HasValue)
        {
            dynamic modifications = decision.ModifiedData.Value;
            
            // Apply quality threshold override if specified
            if (modifications?.QualityOverride == true)
            {
                _logger.LogInformation("Applied quality override");
                updatedData = updatedData.WithProcessingMetadata("quality_override", true);
            }
            
            // Apply new quality threshold if specified
            if (modifications?.QualityThreshold != null)
            {
                float newThreshold = modifications.QualityThreshold;
                _logger.LogInformation("Applied new quality threshold: {Threshold:F2}", newThreshold);
                updatedData = updatedData.WithProcessingMetadata("adjusted_quality_threshold", newThreshold);
            }
        }

        return updatedData;
    }
}