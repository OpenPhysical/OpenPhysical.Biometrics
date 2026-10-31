using System.Collections.Immutable;
using JetBrains.Annotations;

namespace OpenPhysical.Biometrics.Core.Abstractions.Pipeline;

/// <summary>
/// Represents a composable biometric processing pipeline.
/// </summary>
/// <typeparam name="TInput">The input type for the pipeline</typeparam>
/// <typeparam name="TOutput">The output type for the pipeline</typeparam>
[PublicAPI]
public interface IBiometricPipeline<in TInput, TOutput>
{
    /// <summary>
    /// Gets the unique identifier for this pipeline.
    /// </summary>
    PipelineId PipelineId { get; }

    /// <summary>
    /// Gets the human-readable name of this pipeline.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of what this pipeline does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the immutable configuration for this pipeline.
    /// </summary>
    PipelineConfiguration Configuration { get; }

    /// <summary>
    /// Executes the pipeline with the given input.
    /// </summary>
    /// <param name="input">The input data to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A result containing the pipeline execution result</returns>
    Task<Result<PipelineExecutionResult<TOutput>>> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Executes the pipeline with a pre-existing context.
    /// </summary>
    /// <param name="input">The input data to process</param>
    /// <param name="context">The pipeline context to use</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A result containing the pipeline execution result</returns>
    Task<Result<PipelineExecutionResult<TOutput>>> ExecuteAsync(
        TInput input,
        IPipelineContext context,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates if the pipeline can process the given input.
    /// </summary>
    /// <param name="input">The input data to validate</param>
    /// <returns>True if the pipeline can process the input, false otherwise</returns>
    bool CanProcess(TInput input);
}

/// <summary>
/// Immutable record representing a pipeline identifier.
/// </summary>
[PublicAPI]
public readonly record struct PipelineId(string Value)
{
    /// <summary>
    /// Creates a pipeline identifier from a string.
    /// </summary>
    public static implicit operator PipelineId(string value) => new(value);

    /// <summary>
    /// Returns the string representation of this identifier.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Immutable record representing pipeline configuration.
/// </summary>
[PublicAPI]
public readonly record struct PipelineConfiguration(
    ImmutableDictionary<string, object> Settings,
    PipelineExecutionMode ExecutionMode,
    TimeSpan? Timeout
)
{
    /// <summary>
    /// Gets a configuration setting by key.
    /// </summary>
    /// <typeparam name="T">The expected type of the setting</typeparam>
    /// <param name="key">The setting key</param>
    /// <returns>Maybe containing the setting value if found and of correct type</returns>
    public Maybe<T> GetSetting<T>(string key)
    {
        if (!Settings.TryGetValue(key, out var value))
            return Maybe<T>.None;

        return value is T typedValue ? Maybe<T>.From(typedValue) : Maybe<T>.None;
    }

    /// <summary>
    /// Creates a default pipeline configuration.
    /// </summary>
    public static PipelineConfiguration Default =>
        new(
            ImmutableDictionary<string, object>.Empty,
            PipelineExecutionMode.Standard,
            TimeSpan.FromMinutes(5)
        );

    /// <summary>
    /// Returns a new configuration with the specified setting.
    /// </summary>
    public PipelineConfiguration WithSetting(string key, object value) =>
        this with
        {
            Settings = Settings.SetItem(key, value),
        };
}

/// <summary>
/// Enum representing pipeline execution modes.
/// </summary>
[PublicAPI]
public enum PipelineExecutionMode
{
    /// <summary>Standard execution with all steps</summary>
    Standard,

    /// <summary>Interactive execution with checkpoint prompts</summary>
    Interactive,

    /// <summary>Automated execution without human intervention</summary>
    Automated,

    /// <summary>Validation mode for testing pipeline integrity</summary>
    Validation,
}

/// <summary>
/// Immutable record representing the result of a pipeline execution.
/// </summary>
[PublicAPI]
public readonly record struct PipelineExecutionResult<TOutput>(
    TOutput Output,
    IPipelineContext Context,
    TimeSpan Duration,
    PipelineExecutionStatus Status,
    Maybe<string> StatusMessage
)
{
    /// <summary>
    /// Creates a successful pipeline result.
    /// </summary>
    public static PipelineExecutionResult<TOutput> Success(
        TOutput output,
        IPipelineContext context,
        TimeSpan duration
    ) => new(output, context, duration, PipelineExecutionStatus.Completed, Maybe<string>.None);

    /// <summary>
    /// Creates a failed pipeline result.
    /// </summary>
    public static PipelineExecutionResult<TOutput> Failure(
        IPipelineContext context,
        TimeSpan duration,
        string errorMessage
    ) =>
        new(
            default!,
            context,
            duration,
            PipelineExecutionStatus.Failed,
            Maybe<string>.From(errorMessage)
        );

    /// <summary>
    /// Creates an aborted pipeline result.
    /// </summary>
    public static PipelineExecutionResult<TOutput> Aborted(
        IPipelineContext context,
        TimeSpan duration,
        string reason
    ) =>
        new(
            default!,
            context,
            duration,
            PipelineExecutionStatus.Aborted,
            Maybe<string>.From(reason)
        );
}

/// <summary>
/// Enum representing pipeline execution status.
/// </summary>
[PublicAPI]
public enum PipelineExecutionStatus
{
    /// <summary>Pipeline completed successfully</summary>
    Completed,

    /// <summary>Pipeline failed due to an error</summary>
    Failed,

    /// <summary>Pipeline was aborted by user or system</summary>
    Aborted,

    /// <summary>Pipeline execution was cancelled</summary>
    Cancelled,
}
