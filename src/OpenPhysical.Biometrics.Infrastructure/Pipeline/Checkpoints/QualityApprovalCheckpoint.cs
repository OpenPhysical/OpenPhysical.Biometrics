using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using Spectre.Console;
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline.Checkpoints;

/// <summary>
/// Base checkpoint for quality approval decisions in biometric processing.
/// </summary>
[PublicAPI]
public abstract class QualityApprovalCheckpoint : ICheckpoint<QualityMetrics>
{
    protected readonly ILogger<QualityApprovalCheckpoint> _logger;

    /// <summary>
    /// Gets the unique identifier for this checkpoint.
    /// </summary>
    public string CheckpointId => "quality-approval";

    /// <summary>
    /// Gets the human-readable name of this checkpoint.
    /// </summary>
    public string Name => "Quality Approval";

    /// <summary>
    /// Gets the description of the decision required at this checkpoint.
    /// </summary>
    public string Description => "Review biometric quality metrics and decide whether to proceed with processing";

    /// <summary>
    /// Gets the available decision options for this checkpoint.
    /// </summary>
    public ImmutableList<CheckpointOption> AvailableOptions { get; } = ImmutableList.Create(
        new CheckpointOption(
            "approve", 
            "Approve Quality", 
            "Accept the current quality and continue processing",
            CheckpointOptionType.Continue
        ),
        new CheckpointOption(
            "retry-with-adjustments", 
            "Retry with Adjustments", 
            "Retry quality assessment with modified parameters",
            CheckpointOptionType.ContinueWithModifications
        ),
        new CheckpointOption(
            "override-threshold", 
            "Override Threshold", 
            "Continue despite quality concerns with manual override",
            CheckpointOptionType.ContinueWithModifications
        ),
        new CheckpointOption(
            "reject", 
            "Reject Image", 
            "Reject the image due to insufficient quality",
            CheckpointOptionType.Abort
        )
    );

    /// <summary>
    /// Initializes a new instance of the quality approval checkpoint.
    /// </summary>
    protected QualityApprovalCheckpoint(ILogger<QualityApprovalCheckpoint> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Requests a human decision for quality approval.
    /// </summary>
    public async Task<Result<CheckpointResult>> RequestDecisionAsync(
        QualityMetrics data,
        IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Quality approval checkpoint reached for execution {ExecutionId}", 
            context.ExecutionId);

        try
        {
            var decision = await RequestQualityDecisionAsync(data, context, cancellationToken);
            
            _logger.LogInformation(
                "Quality approval decision: {Decision} for execution {ExecutionId}",
                decision.Decision, context.ExecutionId);

            return Result.Success(decision);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Quality approval checkpoint was cancelled");
            return Result.Failure<CheckpointResult>("Quality approval was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quality approval checkpoint failed");
            return Result.Failure<CheckpointResult>($"Quality approval failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Abstract method for implementation-specific quality decision logic.
    /// </summary>
    protected abstract Task<CheckpointResult> RequestQualityDecisionAsync(
        QualityMetrics qualityMetrics,
        IPipelineContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Helper method to format quality metrics for display.
    /// </summary>
    protected static string FormatQualityMetrics(QualityMetrics metrics)
    {
        var details = new List<string>
        {
            $"Overall Quality: {metrics.OverallQuality.Value:F3}"
        };

        if (metrics.SharpnessScore.HasValue)
            details.Add($"Sharpness: {metrics.SharpnessScore.Value.Value:F3}");

        if (metrics.ExposureScore.HasValue)
            details.Add($"Exposure: {metrics.ExposureScore.Value.Value:F3}");

        if (metrics.GeometryScore.HasValue)
            details.Add($"Geometry: {metrics.GeometryScore.Value.Value:F3}");

        return string.Join(", ", details);
    }

    /// <summary>
    /// Determines quality status based on metrics.
    /// </summary>
    protected static QualityStatus GetQualityStatus(QualityMetrics metrics, float threshold = 0.7f)
    {
        var overallQuality = metrics.OverallQuality.Value;

        if (overallQuality >= 0.9f)
            return QualityStatus.Excellent;
        if (overallQuality >= 0.8f)
            return QualityStatus.Good;
        if (overallQuality >= threshold)
            return QualityStatus.Acceptable;
        if (overallQuality >= 0.5f)
            return QualityStatus.Poor;
        
        return QualityStatus.Unacceptable;
    }
}

/// <summary>
/// Console-based implementation of quality approval checkpoint.
/// </summary>
[PublicAPI]
public sealed class ConsoleQualityApprovalCheckpoint : QualityApprovalCheckpoint
{
    private readonly IAnsiConsole _console;

    /// <summary>
    /// Initializes a new instance of the console quality approval checkpoint.
    /// </summary>
    public ConsoleQualityApprovalCheckpoint(
        IAnsiConsole console,
        ILogger<QualityApprovalCheckpoint> logger) : base(logger)
    {
        _console = console;
    }

    /// <summary>
    /// Requests quality decision via console interaction.
    /// </summary>
    protected override async Task<CheckpointResult> RequestQualityDecisionAsync(
        QualityMetrics qualityMetrics,
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        var status = GetQualityStatus(qualityMetrics);
        var qualityText = FormatQualityMetrics(qualityMetrics);

        // Display quality information
        _console.MarkupLine($"[bold]Quality Assessment Results[/]");
        _console.MarkupLine($"Status: {GetStatusMarkup(status)}");
        _console.MarkupLine($"Details: {qualityText}");
        _console.WriteLine();

        // Auto-approve excellent quality
        if (status == QualityStatus.Excellent)
        {
            _console.MarkupLine("[green]Quality is excellent - automatically approved[/]");
            return CheckpointResult.Continue("approve");
        }

        // Auto-reject unacceptable quality
        if (status == QualityStatus.Unacceptable)
        {
            _console.MarkupLine("[red]Quality is unacceptable - automatically rejected[/]");
            return CheckpointResult.Abort("reject", "Quality below minimum acceptable threshold");
        }

        // Prompt for user decision
        var prompt = new SelectionPrompt<string>()
            .Title("How would you like to proceed?");
        prompt.AddChoices(new[] {
            "approve: Accept current quality and continue",
            "retry-with-adjustments: Retry with different parameters",
            "override-threshold: Continue despite quality concerns",
            "reject: Reject image due to quality"
        });
        
        var choice = _console.Prompt(prompt);

        return choice switch
        {
            var s when s.StartsWith("approve:") => CheckpointResult.Continue("approve"),
            var s when s.StartsWith("retry-with-adjustments:") => await RequestAdjustments(),
            var s when s.StartsWith("override-threshold:") => await RequestOverrideReason(),
            var s when s.StartsWith("reject:") => await RequestRejectionReason(),
            _ => CheckpointResult.Abort("reject", "Invalid choice")
        };
    }

    /// <summary>
    /// Requests quality adjustment parameters from user.
    /// </summary>
    private async Task<CheckpointResult> RequestAdjustments()
    {
        var newThreshold = _console.Prompt(
            new TextPrompt<float>("Enter new quality threshold (0.0-1.0):")
                .DefaultValue(0.6f)
                .Validate(t => t is >= 0.0f and <= 1.0f ? ValidationResult.Success() : ValidationResult.Error("Threshold must be between 0.0 and 1.0"))
        );

        var adjustments = new { QualityThreshold = newThreshold };
        return CheckpointResult.ContinueWithModifications("retry-with-adjustments", adjustments, $"Adjusted quality threshold to {newThreshold:F2}");
    }

    /// <summary>
    /// Requests override reason from user.
    /// </summary>
    private async Task<CheckpointResult> RequestOverrideReason()
    {
        var reason = _console.Prompt(
            new TextPrompt<string>("Enter reason for quality override:")
                .Validate(r => !string.IsNullOrWhiteSpace(r) ? ValidationResult.Success() : ValidationResult.Error("Reason is required"))
        );

        var overrideData = new { QualityOverride = true, OverrideReason = reason };
        return CheckpointResult.ContinueWithModifications("override-threshold", overrideData, reason);
    }

    /// <summary>
    /// Requests rejection reason from user.
    /// </summary>
    private async Task<CheckpointResult> RequestRejectionReason()
    {
        var reason = _console.Prompt(
            new TextPrompt<string>("Enter reason for rejection:")
                .DefaultValue("Insufficient image quality")
        );

        return CheckpointResult.Abort("reject", reason);
    }

    /// <summary>
    /// Gets markup color for quality status.
    /// </summary>
    private static string GetStatusMarkup(QualityStatus status) => status switch
    {
        QualityStatus.Excellent => "[green]Excellent[/]",
        QualityStatus.Good => "[lime]Good[/]",
        QualityStatus.Acceptable => "[yellow]Acceptable[/]",
        QualityStatus.Poor => "[orange1]Poor[/]",
        QualityStatus.Unacceptable => "[red]Unacceptable[/]",
        _ => "[gray]Unknown[/]"
    };
}

/// <summary>
/// Enum representing quality assessment status.
/// </summary>
[PublicAPI]
public enum QualityStatus
{
    /// <summary>Excellent quality (>= 0.9)</summary>
    Excellent,
    /// <summary>Good quality (>= 0.8)</summary>
    Good,
    /// <summary>Acceptable quality (>= threshold)</summary>
    Acceptable,
    /// <summary>Poor quality (>= 0.5)</summary>
    Poor,
    /// <summary>Unacceptable quality (&lt; 0.5)</summary>
    Unacceptable
}