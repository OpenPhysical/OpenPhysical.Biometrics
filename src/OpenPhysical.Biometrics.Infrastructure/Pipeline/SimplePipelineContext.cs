using System.Collections.Immutable;
using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline;

/// <summary>
/// Immutable implementation of pipeline context.
/// </summary>
[PublicAPI]
public sealed record SimplePipelineContext : IPipelineContext
{
    /// <inheritdoc />
    public PipelineExecutionId ExecutionId { get; init; }

    /// <inheritdoc />
    public DateTimeOffset StartedAt { get; init; }

    /// <inheritdoc />
    public ImmutableDictionary<string, object> Metadata { get; init; }

    /// <inheritdoc />
    public ImmutableList<PipelineStepTrace> ExecutionTrace { get; init; }

    /// <summary>
    /// Initializes a new instance of the simple pipeline context.
    /// </summary>
    public SimplePipelineContext(
        PipelineExecutionId executionId,
        DateTimeOffset startedAt,
        ImmutableDictionary<string, object> metadata,
        ImmutableList<PipelineStepTrace> executionTrace
    )
    {
        ExecutionId = executionId;
        StartedAt = startedAt;
        Metadata = metadata;
        ExecutionTrace = executionTrace;
    }

    /// <summary>
    /// Creates a new pipeline context with default values.
    /// </summary>
    public static SimplePipelineContext Create() =>
        new(
            PipelineExecutionId.New(),
            DateTimeOffset.UtcNow,
            ImmutableDictionary<string, object>.Empty,
            ImmutableList<PipelineStepTrace>.Empty
        );

    /// <inheritdoc />
    public IPipelineContext WithMetadata(string key, object value) =>
        this with
        {
            Metadata = Metadata.SetItem(key, value),
        };

    /// <inheritdoc />
    public IPipelineContext WithMetadata(ImmutableDictionary<string, object> metadata)
    {
        var newMetadata = Metadata;
        foreach (var kvp in metadata)
        {
            newMetadata = newMetadata.SetItem(kvp.Key, kvp.Value);
        }
        return this with { Metadata = newMetadata };
    }

    /// <inheritdoc />
    public IPipelineContext WithStepTrace(PipelineStepTrace stepTrace) =>
        this with
        {
            ExecutionTrace = ExecutionTrace.Add(stepTrace),
        };

    /// <inheritdoc />
    public Maybe<T> GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return Maybe<T>.From(typedValue);
        }
        return Maybe<T>.None;
    }
}
