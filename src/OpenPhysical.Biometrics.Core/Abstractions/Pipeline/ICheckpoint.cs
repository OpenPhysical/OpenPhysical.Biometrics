using JetBrains.Annotations;
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Core.Abstractions.Pipeline;

/// <summary>
/// Represents a human-in-the-middle checkpoint in a biometric processing pipeline.
/// </summary>
/// <typeparam name="TData">The type of data presented for decision</typeparam>
[PublicAPI]
public interface ICheckpoint<TData>
{
    /// <summary>
    /// Gets the unique identifier for this checkpoint.
    /// </summary>
    string CheckpointId { get; }

    /// <summary>
    /// Gets the human-readable name of this checkpoint.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of the decision required at this checkpoint.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the available decision options for this checkpoint.
    /// </summary>
    ImmutableList<CheckpointOption> AvailableOptions { get; }

    /// <summary>
    /// Requests a human decision at this checkpoint.
    /// </summary>
    /// <param name="data">The data to present for decision</param>
    /// <param name="context">The pipeline context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The checkpoint result containing the user's decision</returns>
    Task<Result<CheckpointResult>> RequestDecisionAsync(
        TData data,
        IPipelineContext context,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Immutable record representing a checkpoint option available to the user.
/// </summary>
[PublicAPI]
public readonly record struct CheckpointOption(
    string OptionId,
    string DisplayName,
    string Description,
    CheckpointOptionType Type
);

/// <summary>
/// Enum representing the type of checkpoint option.
/// </summary>
[PublicAPI]
public enum CheckpointOptionType
{
    /// <summary>Continue processing with current data</summary>
    Continue,
    /// <summary>Continue with user-provided modifications</summary>
    ContinueWithModifications,
    /// <summary>Retry the previous step</summary>
    Retry,
    /// <summary>Skip this checkpoint and continue</summary>
    Skip,
    /// <summary>Abort the pipeline</summary>
    Abort
}

/// <summary>
/// Immutable record representing the result of a checkpoint decision.
/// </summary>
[PublicAPI]
public readonly record struct CheckpointResult(
    string SelectedOptionId,
    CheckpointDecision Decision,
    Maybe<object> ModifiedData,
    Maybe<string> UserComment
)
{
    /// <summary>
    /// Creates a continue decision.
    /// </summary>
    public static CheckpointResult Continue(string optionId) =>
        new(optionId, CheckpointDecision.Continue, Maybe<object>.None, Maybe<string>.None);

    /// <summary>
    /// Creates a continue decision with modifications.
    /// </summary>
    public static CheckpointResult ContinueWithModifications(string optionId, object modifiedData, string? comment = null) =>
        new(optionId, CheckpointDecision.ContinueWithModifications, Maybe<object>.From(modifiedData), Maybe<string>.From(comment));

    /// <summary>
    /// Creates a retry decision.
    /// </summary>
    public static CheckpointResult Retry(string optionId, string? comment = null) =>
        new(optionId, CheckpointDecision.Retry, Maybe<object>.None, Maybe<string>.From(comment));

    /// <summary>
    /// Creates a skip decision.
    /// </summary>
    public static CheckpointResult Skip(string optionId, string? comment = null) =>
        new(optionId, CheckpointDecision.Skip, Maybe<object>.None, Maybe<string>.From(comment));

    /// <summary>
    /// Creates an abort decision.
    /// </summary>
    public static CheckpointResult Abort(string optionId, string reason) =>
        new(optionId, CheckpointDecision.Abort, Maybe<object>.None, Maybe<string>.From(reason));
}

/// <summary>
/// Enum representing the checkpoint decision.
/// </summary>
[PublicAPI]
public enum CheckpointDecision
{
    /// <summary>Continue with current data</summary>
    Continue,
    /// <summary>Continue with user modifications</summary>
    ContinueWithModifications,
    /// <summary>Retry the previous step</summary>
    Retry,
    /// <summary>Skip this checkpoint</summary>
    Skip,
    /// <summary>Abort the pipeline</summary>
    Abort
}