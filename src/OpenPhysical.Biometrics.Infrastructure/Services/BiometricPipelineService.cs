using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Biometrics.Core.Domain.Common;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Infrastructure.Pipeline;
using OpenPhysical.Domains.Biometrics;
using CoreQualityScore = OpenPhysical.Biometrics.Core.Domain.Quality.QualityScore;
using PipelineBiometricFormat = OpenPhysical.Biometrics.Core.Domain.Pipeline.BiometricFormat;

namespace OpenPhysical.Biometrics.Infrastructure.Services;

/// <summary>
/// Service for executing biometric processing pipelines.
/// Provides library-first functionality for complete biometric workflows.
/// </summary>
[PublicAPI]
public interface IBiometricPipelineService
{
    /// <summary>
    /// Executes a predefined pipeline for a specific standard.
    /// </summary>
    Task<Result<BiometricPipelineResult>> ExecuteStandardPipelineAsync(
        ImmutableArray<byte> imageData,
        BiometricStandard standard,
        PipelineExecutionPolicy executionPolicy,
        ImmutableDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a custom pipeline with specified steps.
    /// </summary>
    Task<Result<BiometricPipelineResult>> ExecuteCustomPipelineAsync(
        ImmutableArray<byte> imageData,
        ImmutableList<IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>> steps,
        PipelineExecutionPolicy executionPolicy,
        ImmutableDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a pipeline builder for fluent pipeline construction.
    /// </summary>
    IPipelineBuilder CreatePipelineBuilder();

    /// <summary>
    /// Gets available pipeline steps.
    /// </summary>
    ImmutableDictionary<string, IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>> GetAvailableSteps();
}

/// <summary>
/// Implementation of biometric pipeline service.
/// </summary>
[PublicAPI]
public sealed class BiometricPipelineService : IBiometricPipelineService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BiometricPipelineService> _logger;
    private readonly ImmutableDictionary<string, IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>> _availableSteps;

    /// <summary>
    /// Initializes a new instance of the biometric pipeline service.
    /// </summary>
    public BiometricPipelineService(
        IServiceProvider serviceProvider,
        ILogger<BiometricPipelineService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _availableSteps = InitializeAvailableSteps(serviceProvider);
    }

    /// <inheritdoc />
    public async Task<Result<BiometricPipelineResult>> ExecuteStandardPipelineAsync(
        ImmutableArray<byte> imageData,
        BiometricStandard standard,
        PipelineExecutionPolicy executionPolicy,
        ImmutableDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting {Standard} pipeline execution", standard);

        var steps = GetStandardPipeline(standard);
        return await ExecuteCustomPipelineAsync(imageData, steps, executionPolicy, metadata, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<BiometricPipelineResult>> ExecuteCustomPipelineAsync(
        ImmutableArray<byte> imageData,
        ImmutableList<IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>> steps,
        PipelineExecutionPolicy executionPolicy,
        ImmutableDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting custom pipeline execution with {Count} steps", steps.Count);

            // Create pipeline context with metadata if provided
            var context = metadata != null 
                ? SimplePipelineContext.Create().WithMetadata(metadata) 
                : SimplePipelineContext.Create();

            // Initialize biometric data
            var initialData = new BiometricData(
                imageData,
                BiometricDataType.FacialImage,
                PipelineBiometricFormat.Jpeg,
                ImmutableDictionary<string, object>.Empty);

            var currentData = new ProcessedBiometricData(
                initialData,
                QualityMetrics.WithOverallQuality(CoreQualityScore.Create(0).Value),
                ImmutableList<Core.Domain.Detection.DetectedFace>.Empty,
                Maybe<Core.Domain.Detection.FaceLandmarks68>.None,
                ImmutableDictionary<string, object>.Empty);

            // Execute pipeline steps
            var executedSteps = ImmutableList<ExecutedStep>.Empty;
            var startTime = DateTime.UtcNow;

            foreach (var step in steps)
            {
                _logger.LogDebug("Executing step: {StepId} - {StepName}", step.StepId, step.Name);
                
                var stepStartTime = DateTime.UtcNow;
                
                // Check if step can process current data
                if (!step.CanProcess(currentData))
                {
                    _logger.LogWarning("Step {StepId} cannot process current data, skipping", step.StepId);
                    continue;
                }

                // Execute step
                var stepResult = await step.ExecuteAsync(currentData, context, cancellationToken);
                
                if (stepResult.IsFailure)
                {
                    _logger.LogError("Step {StepId} failed: {Error}", step.StepId, stepResult.Error);
                    
                    if (executionPolicy.StopOnFirstError)
                    {
                        return Result.Failure<BiometricPipelineResult>(
                            $"Pipeline failed at step {step.StepId}: {stepResult.Error}");
                    }

                    // Record failed step and continue
                    executedSteps = executedSteps.Add(new ExecutedStep(
                        step.StepId,
                        step.Name,
                        StepStatus.Failed,
                        DateTime.UtcNow - stepStartTime,
                        Maybe.From(stepResult.Error)));
                    continue;
                }

                // Update current data
                currentData = stepResult.Value;
                
                // Record successful step
                executedSteps = executedSteps.Add(new ExecutedStep(
                    step.StepId,
                    step.Name,
                    StepStatus.Completed,
                    DateTime.UtcNow - stepStartTime,
                    Maybe.None));

                _logger.LogDebug("Step {StepId} completed successfully", step.StepId);

                // Checkpoint handling is controlled by execution policy
                // Individual steps check the policy to determine if checkpoints are enabled
                if (executionPolicy.EnableExecutionTracing)
                {
                    var trace = PipelineStepTrace.Success(
                        step.StepId,
                        step.Name,
                        stepStartTime,
                        DateTime.UtcNow
                    );
                    context = context.WithStepTrace(trace);
                }
            }

            var totalDuration = DateTime.UtcNow - startTime;
            
            // Create pipeline result
            var result = new BiometricPipelineResult(
                context.ExecutionId.ToString(),
                PipelineStatus.Completed,
                currentData,
                executedSteps,
                totalDuration,
                context.Metadata);

            _logger.LogInformation(
                "Pipeline execution completed successfully in {Duration}ms with {StepCount} steps",
                totalDuration.TotalMilliseconds, executedSteps.Count);

            return Result.Success(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Pipeline execution was cancelled");
            return Result.Failure<BiometricPipelineResult>("Pipeline execution was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline execution failed with exception");
            return Result.Failure<BiometricPipelineResult>($"Pipeline execution failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public IPipelineBuilder CreatePipelineBuilder()
    {
        return new PipelineBuilder(_serviceProvider);
    }

    /// <inheritdoc />
    public ImmutableDictionary<string, IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>> GetAvailableSteps()
    {
        return _availableSteps;
    }

    private static ImmutableDictionary<string, IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>> InitializeAvailableSteps(
        IServiceProvider serviceProvider)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>>();

        // Get all registered pipeline steps
        var faceDetectionStep = serviceProvider.GetService<BiometricPipelineSteps.IFaceDetectionStep>();
        if (faceDetectionStep != null)
        {
            builder.Add(faceDetectionStep.StepId, (IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>)faceDetectionStep);
        }

        var landmarkStep = serviceProvider.GetService<BiometricPipelineSteps.ILandmarkExtractionStep>();
        if (landmarkStep != null)
        {
            builder.Add(landmarkStep.StepId, (IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>)landmarkStep);
        }

        var qualityStep = serviceProvider.GetService<BiometricPipelineSteps.IQualityAssessmentStep>();
        if (qualityStep != null)
        {
            builder.Add(qualityStep.StepId, (IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>)qualityStep);
        }

        var transformStep = serviceProvider.GetService<BiometricPipelineSteps.IImageTransformStep>();
        if (transformStep != null)
        {
            builder.Add(transformStep.StepId, (IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>)transformStep);
        }

        var encodingStep = serviceProvider.GetService<BiometricPipelineSteps.IJpeg2000EncodingStep>();
        if (encodingStep != null)
        {
            builder.Add(encodingStep.StepId, (IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>)encodingStep);
        }

        return builder.ToImmutable();
    }

    private ImmutableList<IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>> GetStandardPipeline(
        BiometricStandard standard)
    {
        var builder = ImmutableList.CreateBuilder<IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>>();

        switch (standard)
        {
            case BiometricStandard.Piv:
                // PIV standard pipeline
                if (_availableSteps.TryGetValue("face-detection", out var pivFaceStep))
                    builder.Add(pivFaceStep);
                if (_availableSteps.TryGetValue("landmark-extraction", out var pivLandmarkStep))
                    builder.Add(pivLandmarkStep);
                if (_availableSteps.TryGetValue("quality-assessment", out var pivQualityStep))
                    builder.Add(pivQualityStep);
                if (_availableSteps.TryGetValue("image-transformation", out var pivTransformStep))
                    builder.Add(pivTransformStep);
                if (_availableSteps.TryGetValue("encoding", out var pivEncodingStep))
                    builder.Add(pivEncodingStep);
                break;

            case BiometricStandard.Twic:
                // TWIC standard pipeline (similar but with different parameters)
                if (_availableSteps.TryGetValue("face-detection", out var twicFaceStep))
                    builder.Add(twicFaceStep);
                if (_availableSteps.TryGetValue("quality-assessment", out var twicQualityStep))
                    builder.Add(twicQualityStep);
                if (_availableSteps.TryGetValue("image-transformation", out var twicTransformStep))
                    builder.Add(twicTransformStep);
                if (_availableSteps.TryGetValue("encoding", out var twicEncodingStep))
                    builder.Add(twicEncodingStep);
                break;

            case BiometricStandard.Icao:
                // ICAO standard pipeline for passports
                if (_availableSteps.TryGetValue("face-detection", out var icaoFaceStep))
                    builder.Add(icaoFaceStep);
                if (_availableSteps.TryGetValue("quality-assessment", out var icaoQualityStep))
                    builder.Add(icaoQualityStep);
                if (_availableSteps.TryGetValue("image-transformation", out var icaoTransformStep))
                    builder.Add(icaoTransformStep);
                break;

            default:
                _logger.LogWarning("No standard pipeline defined for {Standard}", standard);
                break;
        }

        return builder.ToImmutable();
    }
}

/// <summary>
/// Builder for creating custom pipelines.
/// </summary>
[PublicAPI]
public interface IPipelineBuilder
{
    /// <summary>
    /// Adds a step to the pipeline.
    /// </summary>
    IPipelineBuilder AddStep(IPipelineStep<ProcessedBiometricData, ProcessedBiometricData> step);

    /// <summary>
    /// Adds a step by ID.
    /// </summary>
    IPipelineBuilder AddStep(string stepId);

    /// <summary>
    /// Adds a conditional step.
    /// </summary>
    IPipelineBuilder AddConditionalStep(
        IPipelineStep<ProcessedBiometricData, ProcessedBiometricData> step,
        Func<ProcessedBiometricData, bool> condition);

    /// <summary>
    /// Adds a checkpoint.
    /// </summary>
    IPipelineBuilder AddCheckpoint(ICheckpoint<ProcessedBiometricData> checkpoint);

    /// <summary>
    /// Builds the pipeline.
    /// </summary>
    ImmutableList<IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>> Build();
}

/// <summary>
/// Implementation of pipeline builder.
/// </summary>
internal sealed class PipelineBuilder : IPipelineBuilder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>> _steps;

    public PipelineBuilder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _steps = new List<IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>>();
    }

    public IPipelineBuilder AddStep(IPipelineStep<ProcessedBiometricData, ProcessedBiometricData> step)
    {
        _steps.Add(step);
        return this;
    }

    public IPipelineBuilder AddStep(string stepId)
    {
        var pipelineService = _serviceProvider.GetRequiredService<IBiometricPipelineService>();
        var availableSteps = pipelineService?.GetAvailableSteps();
        
        if (availableSteps != null && availableSteps.TryGetValue(stepId, out var step))
        {
            _steps.Add(step);
        }
        else
        {
            throw new InvalidOperationException($"Step with ID '{stepId}' not found");
        }
        
        return this;
    }

    public IPipelineBuilder AddConditionalStep(
        IPipelineStep<ProcessedBiometricData, ProcessedBiometricData> step,
        Func<ProcessedBiometricData, bool> condition)
    {
        _steps.Add(new ConditionalPipelineStep(step, condition));
        return this;
    }

    public IPipelineBuilder AddCheckpoint(ICheckpoint<ProcessedBiometricData> checkpoint)
    {
        _steps.Add(new CheckpointStep(checkpoint));
        return this;
    }

    public ImmutableList<IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>> Build()
    {
        return _steps.ToImmutableList();
    }
}

/// <summary>
/// Conditional pipeline step wrapper.
/// </summary>
internal sealed class ConditionalPipelineStep : IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>
{
    private readonly IPipelineStep<ProcessedBiometricData, ProcessedBiometricData> _innerStep;
    private readonly Func<ProcessedBiometricData, bool> _condition;

    public string StepId => _innerStep.StepId;
    public string Name => $"Conditional: {_innerStep.Name}";
    public string Description => _innerStep.Description;

    public ConditionalPipelineStep(
        IPipelineStep<ProcessedBiometricData, ProcessedBiometricData> innerStep,
        Func<ProcessedBiometricData, bool> condition)
    {
        _innerStep = innerStep;
        _condition = condition;
    }

    public Task<Result<ProcessedBiometricData>> ExecuteAsync(
        ProcessedBiometricData input,
        IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (_condition(input))
        {
            return _innerStep.ExecuteAsync(input, context, cancellationToken);
        }

        return Task.FromResult(Result.Success(input));
    }

    public bool CanProcess(ProcessedBiometricData input)
    {
        return _condition(input) && _innerStep.CanProcess(input);
    }
}

/// <summary>
/// Checkpoint step wrapper.
/// </summary>
internal sealed class CheckpointStep : IPipelineStep<ProcessedBiometricData, ProcessedBiometricData>
{
    private readonly ICheckpoint<ProcessedBiometricData> _checkpoint;

    public string StepId => $"checkpoint-{_checkpoint.CheckpointId}";
    public string Name => _checkpoint.Name;
    public string Description => _checkpoint.Description;

    public CheckpointStep(ICheckpoint<ProcessedBiometricData> checkpoint)
    {
        _checkpoint = checkpoint;
    }

    public async Task<Result<ProcessedBiometricData>> ExecuteAsync(
        ProcessedBiometricData input,
        IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var decision = await _checkpoint.RequestDecisionAsync(input, context, cancellationToken);
        
        if (decision.IsFailure)
        {
            return Result.Failure<ProcessedBiometricData>(decision.Error);
        }

        var result = decision.Value;
        
        // Handle different checkpoint decisions
        switch (result.Decision)
        {
            case CheckpointDecision.Continue:
                return Result.Success(input);
                
            case CheckpointDecision.ContinueWithModifications:
                // Apply any modifications from the checkpoint
                if (result.ModifiedData.HasValue && result.ModifiedData.Value is ProcessedBiometricData modifiedData)
                {
                    return Result.Success(modifiedData);
                }
                return Result.Success(input);
                
            case CheckpointDecision.Skip:
                return Result.Success(input);
                
            case CheckpointDecision.Retry:
                // For now, treat retry as continue - more complex retry logic could be added
                return Result.Success(input);
                
            case CheckpointDecision.Abort:
                var reason = result.UserComment.HasValue ? result.UserComment.Value : "User aborted pipeline";
                return Result.Failure<ProcessedBiometricData>($"Pipeline aborted at checkpoint: {reason}");
                
            default:
                return Result.Failure<ProcessedBiometricData>($"Unknown checkpoint decision: {result.Decision}");
        }
    }

    public bool CanProcess(ProcessedBiometricData input) => true;
}


/// <summary>
/// Result of pipeline execution.
/// </summary>
[PublicAPI]
public sealed record BiometricPipelineResult(
    string ExecutionId,
    PipelineStatus Status,
    ProcessedBiometricData FinalData,
    ImmutableList<ExecutedStep> ExecutedSteps,
    TimeSpan Duration,
    ImmutableDictionary<string, object> Metadata
);

/// <summary>
/// Information about an executed step.
/// </summary>
[PublicAPI]
public sealed record ExecutedStep(
    string StepId,
    string StepName,
    StepStatus Status,
    TimeSpan Duration,
    Maybe<string> ErrorMessage
);

/// <summary>
/// Status of pipeline execution.
/// </summary>
[PublicAPI]
public enum PipelineStatus
{
    /// <summary>
    /// Pipeline completed successfully.
    /// </summary>
    Completed,
    
    /// <summary>
    /// Pipeline completed with some failed steps.
    /// </summary>
    PartiallyCompleted,
    
    /// <summary>
    /// Pipeline failed.
    /// </summary>
    Failed,
    
    /// <summary>
    /// Pipeline was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Status of a pipeline step.
/// </summary>
[PublicAPI]
public enum StepStatus
{
    /// <summary>
    /// Step completed successfully.
    /// </summary>
    Completed,
    
    /// <summary>
    /// Step was skipped.
    /// </summary>
    Skipped,
    
    /// <summary>
    /// Step failed.
    /// </summary>
    Failed
}

/// <summary>
/// Supported biometric standards.
/// </summary>
[PublicAPI]
public enum BiometricStandard
{
    /// <summary>
    /// Personal Identity Verification (PIV) standard.
    /// </summary>
    Piv,
    
    /// <summary>
    /// Transportation Worker Identification Credential (TWIC) standard.
    /// </summary>
    Twic,
    
    /// <summary>
    /// International Civil Aviation Organization (ICAO) standard.
    /// </summary>
    Icao,
    
    /// <summary>
    /// Custom standard.
    /// </summary>
    Custom
}