using System.Collections.Immutable;
using JetBrains.Annotations;

namespace OpenPhysical.Biometrics.Core.Abstractions.Pipeline;

/// <summary>
/// Immutable context that carries state through a biometric processing pipeline.
/// </summary>
[PublicAPI]
public interface IPipelineContext
{
    /// <summary>
    /// Gets the unique identifier for this pipeline execution.
    /// </summary>
    PipelineExecutionId ExecutionId { get; }

    /// <summary>
    /// Gets the timestamp when this pipeline execution started.
    /// </summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Gets the immutable metadata associated with this pipeline execution.
    /// </summary>
    ImmutableDictionary<string, object> Metadata { get; }

    /// <summary>
    /// Gets the execution trace of completed steps.
    /// </summary>
    ImmutableList<PipelineStepTrace> ExecutionTrace { get; }

    /// <summary>
    /// Returns a new context with additional metadata.
    /// </summary>
    /// <param name="key">The metadata key</param>
    /// <param name="value">The metadata value</param>
    /// <returns>A new context instance with the added metadata</returns>
    IPipelineContext WithMetadata(string key, object value);

    /// <summary>
    /// Returns a new context with multiple metadata entries.
    /// </summary>
    /// <param name="metadata">The metadata to add</param>
    /// <returns>A new context instance with the added metadata</returns>
    IPipelineContext WithMetadata(ImmutableDictionary<string, object> metadata);

    /// <summary>
    /// Returns a new context with a step trace entry added.
    /// </summary>
    /// <param name="stepTrace">The step trace to add</param>
    /// <returns>A new context instance with the added trace</returns>
    IPipelineContext WithStepTrace(PipelineStepTrace stepTrace);

    /// <summary>
    /// Gets metadata value by key if it exists.
    /// </summary>
    /// <typeparam name="T">The expected type of the metadata value</typeparam>
    /// <param name="key">The metadata key</param>
    /// <returns>Maybe containing the value if found and of correct type</returns>
    Maybe<T> GetMetadata<T>(string key);
}

/// <summary>
/// Immutable record representing a pipeline execution identifier.
/// </summary>
[PublicAPI]
public readonly record struct PipelineExecutionId(Guid Value)
{
    /// <summary>
    /// Creates a new random pipeline execution identifier.
    /// </summary>
    public static PipelineExecutionId New() => new(Guid.NewGuid());

    /// <summary>
    /// Returns the string representation of this identifier.
    /// </summary>
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Immutable record representing the execution trace of a pipeline step.
/// </summary>
[PublicAPI]
public readonly record struct PipelineStepTrace(
    string StepId,
    string StepName,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool IsSuccess,
    Maybe<string> ErrorMessage
)
{
    /// <summary>
    /// Gets the duration of this step execution.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Creates a successful step trace.
    /// </summary>
    public static PipelineStepTrace Success(
        string stepId,
        string stepName,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt
    ) => new(stepId, stepName, startedAt, completedAt, true, Maybe<string>.None);

    /// <summary>
    /// Creates a failed step trace.
    /// </summary>
    public static PipelineStepTrace Failure(
        string stepId,
        string stepName,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        string errorMessage
    ) => new(stepId, stepName, startedAt, completedAt, false, Maybe<string>.From(errorMessage));
}
