using CSharpFunctionalExtensions;
using JetBrains.Annotations;

namespace OpenPhysical.Biometrics.Core.Abstractions.Pipeline;

/// <summary>
/// Represents a single step in a biometric processing pipeline.
/// Each step is a pure function that transforms input to output.
/// </summary>
/// <typeparam name="TIn">The input type for this step</typeparam>
/// <typeparam name="TOut">The output type for this step</typeparam>
[PublicAPI]
public interface IBiometricStep<in TIn, TOut>
{
    /// <summary>
    /// Gets the unique identifier for this step
    /// </summary>
    string StepId { get; }
    
    /// <summary>
    /// Gets a human-readable description of what this step does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Executes the step, transforming input to output
    /// </summary>
    /// <param name="input">The input data</param>
    /// <returns>Result containing either the transformed output or an error</returns>
    Result<TOut> Execute(TIn input);
}