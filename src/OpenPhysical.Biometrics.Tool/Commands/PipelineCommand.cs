using System.ComponentModel;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Infrastructure.Services;
using OpenPhysical.Biometrics.Infrastructure.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Tool.Commands;

/// <summary>
/// Command for executing biometric processing pipelines.
/// </summary>
[Description("Execute a biometric processing pipeline")]
[UsedImplicitly]
public sealed class PipelineCommand : AsyncCommand<PipelineCommand.Settings>
{
    /// <summary>
    /// Command settings.
    /// </summary>
    [UsedImplicitly]
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Input image file path.
        /// </summary>
        [CommandArgument(0, "<INPUT>")]
        [Description("Input image file path")]
        public string InputPath { get; set; } = string.Empty;

        /// <summary>
        /// Output file path.
        /// </summary>
        [CommandOption("-o|--output <PATH>")]
        [Description("Output file path")]
        public string? OutputPath { get; set; }

        /// <summary>
        /// Biometric standard to use.
        /// </summary>
        [CommandOption("-s|--standard <STANDARD>")]
        [Description("Biometric standard (piv, twic, icao)")]
        [DefaultValue("piv")]
        public string Standard { get; set; } = "piv";

        /// <summary>
        /// Enable interactive checkpoints.
        /// </summary>
        [CommandOption("-i|--interactive")]
        [Description("Enable interactive checkpoints for quality approval")]
        public bool Interactive { get; set; }

        /// <summary>
        /// Show detailed progress.
        /// </summary>
        [CommandOption("--verbose")]
        [Description("Show detailed progress information")]
        public bool Verbose { get; set; }

        /// <summary>
        /// Target file size in bytes.
        /// </summary>
        [CommandOption("--target-size <SIZE>")]
        [Description("Target file size in bytes")]
        public int? TargetSize { get; set; }

        /// <summary>
        /// Minimum quality threshold.
        /// </summary>
        [CommandOption("--quality-threshold <THRESHOLD>")]
        [Description("Minimum quality threshold (0.0-1.0)")]
        [DefaultValue(0.7f)]
        public float QualityThreshold { get; set; } = 0.7f;

        /// <summary>
        /// Skip quality assessment.
        /// </summary>
        [CommandOption("--skip-quality")]
        [Description("Skip quality assessment step")]
        public bool SkipQuality { get; set; }

        /// <summary>
        /// Save intermediate results.
        /// </summary>
        [CommandOption("--save-intermediate")]
        [Description("Save intermediate results for debugging")]
        public bool SaveIntermediate { get; set; }
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            // Validate input
            if (!File.Exists(settings.InputPath))
            {
                AnsiConsole.MarkupLine($"[red]Error: Input file '{settings.InputPath}' not found.[/]");
                return 1;
            }

            // Parse standard
            if (!Enum.TryParse<BiometricStandard>(settings.Standard, true, out var standard))
            {
                AnsiConsole.MarkupLine($"[red]Error: Invalid standard '{settings.Standard}'. Valid options: piv, twic, icao[/]");
                return 1;
            }

            // Determine output path
            var outputPath = settings.OutputPath ?? GenerateOutputPath(settings.InputPath, standard);

            // Create service provider
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            // Configure biometric services
            services.AddBiometricServices();
            
            // Configure pipeline defaults if needed
            services.Configure<BiometricPipelineDefaults>(defaults =>
            {
                // Adjust defaults based on settings
                if (settings.QualityThreshold != 0.7f)
                {
                    defaults.DefaultStandards = defaults.DefaultStandards
                        .WithQualityThreshold(settings.QualityThreshold);
                }
            });

            using var serviceProvider = services.BuildServiceProvider();
            var pipelineService = serviceProvider.GetRequiredService<IBiometricPipelineService>();
            var logger = serviceProvider.GetRequiredService<ILogger<PipelineCommand>>();

            // Load image data
            var imageData = ImmutableArray.Create(await File.ReadAllBytesAsync(settings.InputPath));

            // Configure pipeline execution policy
            var executionPolicy = settings.Interactive 
                ? PipelineExecutionPolicy.Interactive 
                : PipelineExecutionPolicy.Automated;
                
            // Override specific settings if verbose mode
            if (settings.Verbose)
            {
                executionPolicy = executionPolicy.WithErrorHandling(stopOnFirstError: false);
            }
            
            // Pipeline metadata
            var metadata = ImmutableDictionary<string, object>.Empty
                .Add("Source", settings.InputPath)
                .Add("TargetSize", settings.TargetSize ?? 0)
                .Add("QualityThreshold", settings.QualityThreshold)
                .Add("SkipQuality", settings.SkipQuality)
                .Add("SaveIntermediate", settings.SaveIntermediate)
                .Add("OutputPath", outputPath);

            // Execute pipeline with progress
            Result<BiometricPipelineResult>? result = null;
            
            if (settings.Verbose)
            {
                // Verbose mode - show each step
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule($"[blue]{standard} Pipeline Execution[/]"));
                
                result = await ExecuteWithDetailedProgress(
                    pipelineService, imageData, standard, executionPolicy, metadata, logger);
            }
            else
            {
                // Normal mode - use status display
                await AnsiConsole.Status()
                    .StartAsync($"Executing {standard} pipeline...", async ctx =>
                    {
                        var execResult = await pipelineService.ExecuteStandardPipelineAsync(
                            imageData, standard, executionPolicy, metadata);
                        result = execResult;
                            
                        if (execResult.IsSuccess)
                        {
                            ctx.Status($"[green]Pipeline completed successfully[/]");
                        }
                        else
                        {
                            ctx.Status($"[red]Pipeline failed: {execResult.Error}[/]");
                        }
                    });
            }

            if (result == null)
            {
                AnsiConsole.MarkupLine("[red]Pipeline execution failed: Unknown error[/]");
                return 1;
            }
            
            // Now result is not null, we can access its properties
            if (result.Value.IsFailure)
            {
                AnsiConsole.MarkupLine($"[red]Pipeline execution failed: {result.Value.Error}[/]");
                return 1;
            }

            // At this point result is successful
            // Extract the successful result value
            var pipelineResult = result.Value.Value;
            var finalData = pipelineResult.FinalData;
            if (finalData.ProcessingMetadata.TryGetValue("encoded_data", out var encodedData) && 
                encodedData is byte[])
            {
                var encodedBytes = (byte[])encodedData;
                await File.WriteAllBytesAsync(outputPath, encodedBytes);
                AnsiConsole.MarkupLine($"[green]✓ Output saved to: {outputPath}[/]");
                AnsiConsole.MarkupLine($"[green]  Size: {encodedBytes.Length:N0} bytes[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Warning: No encoded output data found[/]");
            }

            // Show summary
            ShowPipelineSummary(pipelineResult);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static async Task<Result<BiometricPipelineResult>> ExecuteWithDetailedProgress(
        IBiometricPipelineService pipelineService,
        ImmutableArray<byte> imageData,
        BiometricStandard standard,
        PipelineExecutionPolicy executionPolicy,
        ImmutableDictionary<string, object> metadata,
        ILogger<PipelineCommand> logger)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Step")
            .AddColumn("Status")
            .AddColumn("Duration");

        AnsiConsole.Live(table)
            .Start(liveContext =>
            {
                // This would need to be enhanced to show real-time progress
                // For now, just execute the pipeline
                liveContext.Refresh();
            });

        var result = await pipelineService.ExecuteStandardPipelineAsync(
            imageData, standard, executionPolicy, metadata);

        return result;
    }

    private static void ShowPipelineSummary(BiometricPipelineResult result)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]Pipeline Summary[/]"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Step")
            .AddColumn("Status")
            .AddColumn("Duration");

        foreach (var step in result.ExecutedSteps)
        {
            var statusMarkup = step.Status switch
            {
                StepStatus.Completed => "[green]✓ Completed[/]",
                StepStatus.Failed => "[red]✗ Failed[/]",
                StepStatus.Skipped => "[yellow]- Skipped[/]",
                _ => "[gray]Unknown[/]"
            };

            table.AddRow(
                step.StepName,
                statusMarkup,
                $"{step.Duration.TotalMilliseconds:F0}ms");

            if (step.ErrorMessage.HasValue)
            {
                table.AddRow(
                    string.Empty,
                    $"[red]{step.ErrorMessage.Value}[/]",
                    string.Empty);
            }
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Total Duration:[/] {result.Duration.TotalMilliseconds:F0}ms");
        AnsiConsole.MarkupLine($"[bold]Pipeline Status:[/] {GetStatusMarkup(result.Status)}");
    }

    private static string GetStatusMarkup(PipelineStatus status) => status switch
    {
        PipelineStatus.Completed => "[green]Completed[/]",
        PipelineStatus.PartiallyCompleted => "[yellow]Partially Completed[/]",
        PipelineStatus.Failed => "[red]Failed[/]",
        PipelineStatus.Cancelled => "[gray]Cancelled[/]",
        _ => "[gray]Unknown[/]"
    };

    private static string GenerateOutputPath(string inputPath, BiometricStandard standard)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? ".";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var extension = standard == BiometricStandard.Icao ? "jpg" : "jp2";
        
        return Path.Combine(directory, $"{nameWithoutExt}.{standard.ToString().ToLower()}.{extension}");
    }
}