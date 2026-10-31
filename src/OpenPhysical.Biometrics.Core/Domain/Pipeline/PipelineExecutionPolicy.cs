using JetBrains.Annotations;

namespace OpenPhysical.Biometrics.Core.Domain.Pipeline;

/// <summary>
/// Immutable value object defining the execution policy for biometric pipelines.
/// Represents how a pipeline should behave during execution, including error handling,
/// checkpoints, tracing, and timeouts.
/// </summary>
[PublicAPI]
public sealed record PipelineExecutionPolicy(
    bool StopOnFirstError,
    bool EnableCheckpoints,
    bool EnableExecutionTracing,
    TimeSpan ExecutionTimeout
)
{
    /// <summary>
    /// Default interactive execution policy.
    /// Suitable for attended operations where user input may be required.
    /// </summary>
    public static PipelineExecutionPolicy Interactive { get; } =
        new(
            StopOnFirstError: false,
            EnableCheckpoints: true,
            EnableExecutionTracing: true,
            ExecutionTimeout: TimeSpan.FromMinutes(10)
        );

    /// <summary>
    /// Default automated execution policy.
    /// Suitable for unattended batch processing with fast-fail behavior.
    /// </summary>
    public static PipelineExecutionPolicy Automated { get; } =
        new(
            StopOnFirstError: true,
            EnableCheckpoints: false,
            EnableExecutionTracing: false,
            ExecutionTimeout: TimeSpan.FromMinutes(2)
        );

    /// <summary>
    /// Debug execution policy with maximum visibility.
    /// Suitable for development and troubleshooting.
    /// </summary>
    public static PipelineExecutionPolicy Debug { get; } =
        new(
            StopOnFirstError: false,
            EnableCheckpoints: true,
            EnableExecutionTracing: true,
            ExecutionTimeout: TimeSpan.FromMinutes(30)
        );

    /// <summary>
    /// Production execution policy with balanced settings.
    /// Suitable for production workloads with moderate timeout.
    /// </summary>
    public static PipelineExecutionPolicy Production { get; } =
        new(
            StopOnFirstError: true,
            EnableCheckpoints: false,
            EnableExecutionTracing: true,
            ExecutionTimeout: TimeSpan.FromMinutes(5)
        );

    /// <summary>
    /// Validates that the execution timeout is positive.
    /// </summary>
    public bool IsValid => ExecutionTimeout > TimeSpan.Zero;

    /// <summary>
    /// Creates a policy with adjusted error handling behavior.
    /// </summary>
    public PipelineExecutionPolicy WithErrorHandling(bool stopOnFirstError) =>
        this with
        {
            StopOnFirstError = stopOnFirstError,
        };

    /// <summary>
    /// Creates a policy with adjusted checkpoint settings.
    /// </summary>
    public PipelineExecutionPolicy WithCheckpoints(bool enableCheckpoints) =>
        this with
        {
            EnableCheckpoints = enableCheckpoints,
        };

    /// <summary>
    /// Creates a policy with adjusted tracing settings.
    /// </summary>
    public PipelineExecutionPolicy WithTracing(bool enableTracing) =>
        this with
        {
            EnableExecutionTracing = enableTracing,
        };

    /// <summary>
    /// Creates a policy with adjusted timeout.
    /// </summary>
    public PipelineExecutionPolicy WithTimeout(TimeSpan timeout) =>
        this with
        {
            ExecutionTimeout = timeout,
        };

    /// <summary>
    /// Creates a policy suitable for unit testing with minimal timeouts and no checkpoints.
    /// </summary>
    public static PipelineExecutionPolicy ForTesting() =>
        new(
            StopOnFirstError: true,
            EnableCheckpoints: false,
            EnableExecutionTracing: false,
            ExecutionTimeout: TimeSpan.FromSeconds(30)
        );
}
