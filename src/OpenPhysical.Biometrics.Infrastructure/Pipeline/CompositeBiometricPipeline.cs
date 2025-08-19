using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Infrastructure.Pipeline.Steps;
using System.Diagnostics;
using static OpenPhysical.Biometrics.Core.Domain.Pipeline.BiometricPipelineSteps;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline;

/// <summary>
/// Composite implementation of a biometric pipeline that orchestrates multiple processing steps.
/// </summary>
[PublicAPI]
public sealed class CompositeBiometricPipeline<TInput, TOutput> : IBiometricPipeline<TInput, TOutput>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CompositeBiometricPipeline<TInput, TOutput>> _logger;

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
    /// Initializes a new instance of the composite biometric pipeline.
    /// </summary>
    public CompositeBiometricPipeline(
        PipelineId pipelineId,
        string name,
        string description,
        PipelineConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<CompositeBiometricPipeline<TInput, TOutput>> logger)
    {
        PipelineId = pipelineId;
        Name = name;
        Description = description;
        Configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Executes the pipeline with the given input.
    /// </summary>
    public async Task<Result<PipelineExecutionResult<TOutput>>> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default)
    {
        var context = PipelineContext.Create();
        return await ExecuteAsync(input, context, cancellationToken);
    }

    /// <summary>
    /// Executes the pipeline with a pre-existing context.
    /// </summary>
    public async Task<Result<PipelineExecutionResult<TOutput>>> ExecuteAsync(
        TInput input,
        IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting pipeline execution: {PipelineId} ({Name})", PipelineId, Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!CanProcess(input))
            {
                var error = $"Pipeline {PipelineId} cannot process input of type {typeof(TInput).Name}";
                _logger.LogError(error);
                return Result.Failure<PipelineExecutionResult<TOutput>>(error);
            }

            // Determine execution strategy based on pipeline type
            var result = PipelineId.Value switch
            {
                "piv-processing" => await ExecutePivPipelineAsync(input, context, cancellationToken),
                "quality-assessment" => await ExecuteQualityAssessmentPipelineAsync(input, context, cancellationToken),
                _ => throw new NotSupportedException($"Pipeline {PipelineId} is not supported")
            };

            stopwatch.Stop();

            if (result.IsFailure)
            {
                _logger.LogError(
                    "Pipeline execution failed after {Duration}ms: {Error}",
                    stopwatch.ElapsedMilliseconds, result.Error);

                return Result.Success(PipelineExecutionResult<TOutput>.Failure(
                    context, stopwatch.Elapsed, result.Error));
            }

            _logger.LogInformation(
                "Pipeline execution completed successfully in {Duration}ms",
                stopwatch.ElapsedMilliseconds);

            return Result.Success(PipelineExecutionResult<TOutput>.Success(
                result.Value, context, stopwatch.Elapsed));
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("Pipeline execution was cancelled after {Duration}ms", stopwatch.ElapsedMilliseconds);
            
            return Result.Success(PipelineExecutionResult<TOutput>.Aborted(
                context, stopwatch.Elapsed, "Pipeline execution was cancelled"));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Pipeline execution failed with exception after {Duration}ms", stopwatch.ElapsedMilliseconds);
            
            return Result.Success(PipelineExecutionResult<TOutput>.Failure(
                context, stopwatch.Elapsed, $"Pipeline execution failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Validates if the pipeline can process the given input.
    /// </summary>
    public bool CanProcess(TInput input)
    {
        // For BiometricData inputs, check if it's a facial image
        if (input is BiometricData biometricData)
        {
            return biometricData.DataType == BiometricDataType.FacialImage;
        }

        // For other types, assume they can be processed
        return true;
    }

    /// <summary>
    /// Executes the PIV processing pipeline steps.
    /// </summary>
    private async Task<Result<TOutput>> ExecutePivPipelineAsync(
        TInput input,
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        if (input is not BiometricData biometricData)
        {
            return Result.Failure<TOutput>("PIV pipeline requires BiometricData input");
        }

        try
        {
            // Step 1: Face Detection
            var faceDetectionStep = CreateFaceDetectionStep();
            var faceResult = await ExecuteStepWithTracing(
                faceDetectionStep, biometricData, context, cancellationToken);
            
            if (faceResult.IsFailure)
                return Result.Failure<TOutput>(faceResult.Error);

            // Step 2: Quality Assessment
            var qualityStep = CreateQualityAssessmentStep();
            var qualityResult = await ExecuteStepWithTracing(
                qualityStep, faceResult.Value, context, cancellationToken);
            
            if (qualityResult.IsFailure)
                return Result.Failure<TOutput>(qualityResult.Error);

            // Step 3: Landmark Extraction
            var landmarkStep = CreateLandmarkExtractionStep();
            var landmarkResult = await ExecuteStepWithTracing(
                landmarkStep, qualityResult.Value, context, cancellationToken);
            
            if (landmarkResult.IsFailure)
                return Result.Failure<TOutput>(landmarkResult.Error);

            // Convert result to expected output type
            if (landmarkResult.Value is TOutput output)
            {
                return Result.Success(output);
            }

            return Result.Failure<TOutput>("Pipeline output type conversion failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PIV pipeline execution failed");
            return Result.Failure<TOutput>($"PIV pipeline failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the quality assessment pipeline steps.
    /// </summary>
    private async Task<Result<TOutput>> ExecuteQualityAssessmentPipelineAsync(
        TInput input,
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        if (input is not BiometricData biometricData)
        {
            return Result.Failure<TOutput>("Quality assessment pipeline requires BiometricData input");
        }

        try
        {
            // Step 1: Face Detection
            var faceDetectionStep = CreateFaceDetectionStep();
            var faceResult = await ExecuteStepWithTracing(
                faceDetectionStep, biometricData, context, cancellationToken);
            
            if (faceResult.IsFailure)
                return Result.Failure<TOutput>(faceResult.Error);

            // Step 2: Quality Assessment
            var qualityStep = CreateQualityAssessmentStep();
            var qualityResult = await ExecuteStepWithTracing(
                qualityStep, faceResult.Value, context, cancellationToken);
            
            if (qualityResult.IsFailure)
                return Result.Failure<TOutput>(qualityResult.Error);

            // Convert result to expected output type
            if (qualityResult.Value is TOutput output)
            {
                return Result.Success(output);
            }

            return Result.Failure<TOutput>("Pipeline output type conversion failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quality assessment pipeline execution failed");
            return Result.Failure<TOutput>($"Quality assessment pipeline failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a pipeline step with execution tracing.
    /// </summary>
    private async Task<Result<ProcessedBiometricData>> ExecuteStepWithTracing<TStepInput>(
        IPipelineStep<TStepInput, ProcessedBiometricData> step,
        TStepInput input,
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        _logger.LogDebug("Executing step: {StepId} ({StepName})", step.StepId, step.Name);

        try
        {
            var result = await step.ExecuteAsync(input, context, cancellationToken);
            var endTime = DateTimeOffset.UtcNow;

            if (result.IsSuccess)
            {
                var trace = PipelineStepTrace.Success(step.StepId, step.Name, startTime, endTime);
                context = context.WithStepTrace(trace);
                _logger.LogDebug("Step completed successfully: {StepId} in {Duration}ms", 
                    step.StepId, (endTime - startTime).TotalMilliseconds);
            }
            else
            {
                var trace = PipelineStepTrace.Failure(step.StepId, step.Name, startTime, endTime, result.Error);
                context = context.WithStepTrace(trace);
                _logger.LogError("Step failed: {StepId} - {Error}", step.StepId, result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            var endTime = DateTimeOffset.UtcNow;
            var trace = PipelineStepTrace.Failure(step.StepId, step.Name, startTime, endTime, ex.Message);
            context = context.WithStepTrace(trace);
            _logger.LogError(ex, "Step failed with exception: {StepId}", step.StepId);
            return Result.Failure<ProcessedBiometricData>($"Step {step.StepId} failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a face detection step with configuration.
    /// </summary>
    private IFaceDetectionStep CreateFaceDetectionStep()
    {
        var confidence = Configuration.GetSetting<float>("face_detection_confidence").GetValueOrDefault(0.8f);
        var allowMultiple = Configuration.GetSetting<bool>("allow_multiple_faces").GetValueOrDefault(false);

        return new FaceDetectionStep(
            _serviceProvider.GetRequiredService<IFaceDetector>(),
            _serviceProvider.GetRequiredService<ILogger<FaceDetectionStep>>(),
            confidence,
            allowMultiple
        );
    }

    /// <summary>
    /// Creates a quality assessment step with configuration.
    /// </summary>
    private IQualityAssessmentStep CreateQualityAssessmentStep()
    {
        var threshold = Configuration.GetSetting<float>("quality_threshold").GetValueOrDefault(0.7f);
        var standard = Configuration.GetSetting<QualityStandard>("quality_standard").GetValueOrDefault(QualityStandard.NistSp800_76);

        return new QualityAssessmentStep(
            _serviceProvider.GetRequiredService<IQualityAssessor>(),
            _serviceProvider.GetRequiredService<ILogger<QualityAssessmentStep>>(),
            standard,
            threshold
        );
    }

    /// <summary>
    /// Creates a landmark extraction step with configuration.
    /// </summary>
    private ILandmarkExtractionStep CreateLandmarkExtractionStep()
    {
        return new LandmarkExtractionStep(
            _serviceProvider.GetRequiredService<ILandmarkExtractor>(),
            _serviceProvider.GetRequiredService<ILogger<LandmarkExtractionStep>>()
        );
    }
}