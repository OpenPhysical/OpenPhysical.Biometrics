using JetBrains.Annotations;

namespace OpenPhysical.Biometrics.Core.Abstractions.Pipeline;

/// <summary>
/// Represents a step in a biometric processing pipeline that transforms input to output.
/// </summary>
/// <typeparam name="TInput">The input type for this step</typeparam>
/// <typeparam name="TOutput">The output type for this step</typeparam>
[PublicAPI]
public interface IPipelineStep<in TInput, TOutput>
{
    /// <summary>
    /// Gets the unique identifier for this pipeline step.
    /// </summary>
    string StepId { get; }

    /// <summary>
    /// Gets the human-readable name of this step.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of what this step does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes this pipeline step with the given input and context.
    /// </summary>
    /// <param name="input">The input data for this step</param>
    /// <param name="context">The pipeline context for sharing state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A result containing the output or error information</returns>
    Task<Result<TOutput>> ExecuteAsync(
        TInput input,
        IPipelineContext context,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Determines if this step can process the given input.
    /// </summary>
    /// <param name="input">The input data to validate</param>
    /// <returns>True if this step can process the input, false otherwise</returns>
    bool CanProcess(TInput input);
}
