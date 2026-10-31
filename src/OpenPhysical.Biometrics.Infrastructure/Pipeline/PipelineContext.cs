using System.Collections.Immutable;
using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline;

/// <summary>
/// Immutable implementation of pipeline context for carrying state through biometric processing.
/// </summary>
[PublicAPI]
public sealed record PipelineContext(
    PipelineExecutionId ExecutionId,
    DateTimeOffset StartedAt,
    ImmutableDictionary<string, object> Metadata,
    ImmutableList<PipelineStepTrace> ExecutionTrace
) : IPipelineContext
{
    /// <summary>
    /// Creates a new pipeline context for a new execution.
    /// </summary>
    public static PipelineContext Create() =>
        new(
            PipelineExecutionId.New(),
            DateTimeOffset.UtcNow,
            ImmutableDictionary<string, object>.Empty,
            ImmutableList<PipelineStepTrace>.Empty
        );

    /// <summary>
    /// Creates a pipeline context with initial metadata.
    /// </summary>
    public static PipelineContext Create(ImmutableDictionary<string, object> initialMetadata) =>
        new(
            PipelineExecutionId.New(),
            DateTimeOffset.UtcNow,
            initialMetadata,
            ImmutableList<PipelineStepTrace>.Empty
        );

    /// <summary>
    /// Returns a new context with additional metadata.
    /// </summary>
    public IPipelineContext WithMetadata(string key, object value) =>
        this with
        {
            Metadata = Metadata.SetItem(key, value),
        };

    /// <summary>
    /// Returns a new context with multiple metadata entries.
    /// </summary>
    public IPipelineContext WithMetadata(ImmutableDictionary<string, object> metadata) =>
        this with
        {
            Metadata = Metadata.SetItems(metadata),
        };

    /// <summary>
    /// Returns a new context with a step trace entry added.
    /// </summary>
    public IPipelineContext WithStepTrace(PipelineStepTrace stepTrace) =>
        this with
        {
            ExecutionTrace = ExecutionTrace.Add(stepTrace),
        };

    /// <summary>
    /// Gets metadata value by key if it exists.
    /// </summary>
    public Maybe<T> GetMetadata<T>(string key)
    {
        if (!Metadata.TryGetValue(key, out var value))
            return Maybe<T>.None;

        return value is T typedValue ? Maybe<T>.From(typedValue) : Maybe<T>.None;
    }
}
