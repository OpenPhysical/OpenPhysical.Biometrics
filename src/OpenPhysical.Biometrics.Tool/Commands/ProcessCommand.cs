using System.ComponentModel;
using System.Text.Json;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Infrastructure.Extensions;
using OpenPhysical.Biometrics.Infrastructure.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenPhysical.Biometrics.Tool.Commands;

/// <summary>
/// Result containing both processing result and optional quality assessment
/// </summary>
internal record ProcessingResultWithQuality(
    ProcessingResultDto ProcessingResult,
    Iso19794Assessment? QualityAssessment
);

/// <summary>
/// Clean PIV command - just: input image → PIV compliant output.
/// Fully functional implementation using Result&lt;T&gt; pattern with no exceptions.
/// </summary>
/// <remarks>
/// Processes an input image for PIV usage using functional programming principles
/// </remarks>
[Description("Process image for PIV compliance")]
public sealed class ProcessCommand(
    IBiometricProcessingService processingService,
    ILogger<ProcessCommand> logger
) : AsyncCommand<ProcessCommand.Settings>
{
    /// <inheritdoc />
    [UsedImplicitly]
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Input file path
        /// </summary>
        [CommandArgument(0, "<INPUT>")]
        [Description("Input image file path")]
        [UsedImplicitly]
        public string InputPath { get; set; } = string.Empty;

        /// <summary>
        /// Output File Path
        /// </summary>
        [CommandOption("-o|--output <PATH>")]
        [Description("Output file path")]
        [UsedImplicitly]
        public string? OutputPath { get; set; }

        /// <summary>
        /// JPEG 2000 base compression rate in bits per pixel
        /// </summary>
        [CommandOption("--rate <RATE>")]
        [Description("JPEG 2000 base compression rate in bits per pixel")]
        [DefaultValue(0.7f)]
        public float BaseRate { get; set; } = 0.7f;

        /// <summary>
        /// ROI resolution level priority: 0=aggressive, 1=balanced, 2=conservative
        /// </summary>
        [CommandOption("--roi-level <LEVEL>")]
        [Description(
            "ROI resolution level priority: 0=aggressive, 1=balanced, 2=conservative, 3=smoothest"
        )]
        [DefaultValue(3)]
        public int RoiStartLevel { get; set; } = 3;

        /// <summary>
        /// If true, disable ROI encoding (enabled by default with level 3)
        /// </summary>
        [CommandOption("--no-roi")]
        [Description("Disable ROI encoding for uniform quality")]
        public bool NoRoi { get; set; }

        /// <summary>
        /// If true, enable ROI alignment with blocks (disabled by default for smoothest transitions)
        /// </summary>
        [CommandOption("--align")]
        [Description("Enable ROI alignment with compression blocks (may create harsh boundaries)")]
        public bool Align { get; set; }

        /// <summary>
        /// Show detailed processing information
        /// </summary>
        [CommandOption("--verbose")]
        [Description("Show detailed processing information")]
        public bool Verbose { get; set; }

        /// <summary>
        /// Enable debug logging
        /// </summary>
        [CommandOption("--debug")]
        [Description("Enable debug logging for troubleshooting")]
        public bool Debug { get; set; }

        /// <summary>
        /// Processing preset to use
        /// </summary>
        [CommandOption("--preset <PRESET>")]
        [Description(
            "Processing preset: piv-high (30KB), piv-balanced (20KB), twic-max (14KB), piv-min (12KB)"
        )]
        public string? Preset { get; set; }

        /// <summary>
        /// Target file size in bytes (overrides preset and rate)
        /// </summary>
        [CommandOption("--target-size <SIZE>")]
        [Description("Target file size in bytes (overrides preset and rate)")]
        public int? TargetSize { get; set; }

        /// <summary>
        /// Minimum face detection confidence
        /// </summary>
        [CommandOption("--min-confidence <CONFIDENCE>")]
        [Description("Minimum face detection confidence (0.0 to 1.0)")]
        [DefaultValue(0.8f)]
        public float MinConfidence { get; set; } = 0.8f;

        /// <summary>
        /// Maximum rotation angle for face alignment
        /// </summary>
        [CommandOption("--max-rotation <DEGREES>")]
        [Description("Maximum rotation angle in degrees for face alignment")]
        [DefaultValue(15.0f)]
        public float MaxRotation { get; set; } = 15.0f;

        /// <summary>
        /// Skip face detection and PIV transformation
        /// </summary>
        [CommandOption("--no-resize")]
        [Description("Skip face detection and PIV transformation, use image as-is")]
        public bool NoResize { get; set; }

        /// <summary>
        /// Output format
        /// </summary>
        [CommandOption("--format <FORMAT>")]
        [Description(
            "Output format: jp2 (default), jpeg, png, or tiff. Non-JP2 formats require --no-roi"
        )]
        [DefaultValue("jp2")]
        public string Format { get; set; } = "jp2";

        /// <summary>
        /// JPEG quality setting
        /// </summary>
        [CommandOption("--jpeg-quality <QUALITY>")]
        [Description("JPEG quality (1-100, default: 85)")]
        [DefaultValue(85)]
        public int JpegQuality { get; set; } = 85;

        /// <summary>
        /// Minimum quality score (0.0-1.0) required for processing
        /// </summary>
        [CommandOption("--quality-gate <SCORE>")]
        [Description("Minimum quality score (0.0-1.0) required for processing")]
        public float? QualityGate { get; set; }

        /// <summary>
        /// Generate quality assessment report
        /// </summary>
        [CommandOption("--quality-report")]
        [Description("Generate quality assessment report")]
        public bool GenerateQualityReport { get; set; }

        /// <summary>
        /// Quality assessment standard (piv, twic, icao, cac)
        /// </summary>
        [CommandOption("--quality-standard <STANDARD>")]
        [Description("Quality assessment standard (piv, twic, icao, cac)")]
        [DefaultValue("piv")]
        public string QualityStandard { get; set; } = "piv";

        /// <summary>
        /// Apply strict quality requirements (0.85 minimum)
        /// </summary>
        [CommandOption("--strict")]
        [Description("Apply strict quality requirements (0.85 minimum)")]
        public bool Strict { get; set; }
    }

    private readonly IBiometricProcessingService _processingService = processingService;
    private readonly ILogger<ProcessCommand> _logger = logger;

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var validationResult = ValidateInputs(settings);
        if (validationResult.IsFailure)
        {
            _logger.LogError(
                new EventId(3600, "ProcessingFailure"),
                "Image processing reported a failure."
            );
            AnsiConsole.MarkupLine($"[red]✗ {validationResult.Error}[/]");
            return 1;
        }

        var processingResult = await ProcessImageWithSettings(validationResult.Value);
        if (processingResult.IsFailure)
        {
            _logger.LogError(
                new EventId(3601, "ProcessingFailure"),
                "Image processing reported a failure."
            );
            AnsiConsole.MarkupLine($"[red]✗ {processingResult.Error}[/]");
            return 1;
        }

        return 0;
    }

    private Result<Settings> ValidateInputs(Settings settings)
    {
        // Validate input file exists
        if (!File.Exists(settings.InputPath))
        {
            _logger.LogError(
                new EventId(3602, "ProcessingFailure"),
                "Image processing reported a failure."
            );
            return Result.Failure<Settings>($"Input file '{settings.InputPath}' not found.");
        }

        // Validate quality gate range
        if (
            settings.QualityGate.HasValue
            && (settings.QualityGate.Value < 0.0f || settings.QualityGate.Value > 1.0f)
        )
        {
            _logger.LogError(
                new EventId(3603, "ProcessingFailure"),
                "Image processing reported a failure."
            );
            return Result.Failure<Settings>(
                $"Invalid quality gate value '{settings.QualityGate}'. Must be between 0.0 and 1.0."
            );
        }

        // Apply strict mode override
        if (settings.Strict)
        {
            settings.QualityGate = 0.85f;
            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine("[yellow]Strict mode enabled: quality gate set to 0.85[/]");
            }
        }

        // Validate output format
        var validFormats = new[] { "jp2", "jpeg", "jpg", "png", "tiff", "tif" };
        if (!validFormats.Contains(settings.Format, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogError(
                new EventId(3604, "ProcessingFailure"),
                "Image processing reported a failure."
            );
            return Result.Failure<Settings>(
                $"Invalid output format '{settings.Format}'. Must be one of: {string.Join(", ", validFormats)}."
            );
        }

        // Validate ROI requirements for non-JP2 formats
        var isJp2Format = settings.Format.Equals("jp2", StringComparison.OrdinalIgnoreCase);
        if (!isJp2Format && !settings.NoRoi)
        {
            _logger.LogError(
                new EventId(3605, "ProcessingFailure"),
                "Image processing reported a failure."
            );
            return Result.Failure<Settings>(
                $"ROI encoding is not supported for {settings.Format.ToUpperInvariant()} format. You must use the --no-roi flag."
            );
        }

        return Result.Success(settings);
    }

    private async Task<Result> ProcessImageWithSettings(Settings settings)
    {
        // Determine the output path
        var outputPath =
            settings.OutputPath ?? GenerateOutputPath(settings.InputPath, settings.Format);
        _logger.LogDebug(new EventId(695575, "ProcessCommand"), "ProcessCommand stage completed.");

        // Determine processing options based on parameters
        var processingOptions = DetermineProcessingOptions(settings, _logger);

        var imageResult = await LoadImageData(settings.InputPath);
        if (imageResult.IsFailure)
            return Result.Failure(imageResult.Error);

        var processResult = await ProcessImage(imageResult.Value, processingOptions, settings);
        if (processResult.IsFailure)
            return Result.Failure(processResult.Error);

        var saveResult = await SaveResult(
            processResult.Value.ProcessingResult,
            outputPath,
            settings,
            processResult.Value.QualityAssessment
        );
        if (saveResult.IsFailure)
            return Result.Failure(saveResult.Error);

        _logger.LogInformation("Image processing completed successfully");
        AnsiConsole.MarkupLine("[green]✓ Face processing completed successfully![/]");
        return Result.Success();
    }

    private async Task<Result<byte[]>> LoadImageData(string inputPath)
    {
        return await Result.Try(
            async () =>
            {
                AnsiConsole.MarkupLine("[grey]Loading image...[/]");
                var imageData = await File.ReadAllBytesAsync(inputPath);
                _logger.LogDebug(
                    new EventId(641072, "ProcessCommand"),
                    "ProcessCommand stage completed."
                );
                return imageData;
            },
            ex => $"Failed to load image: {ex.Message}"
        );
    }

    private async Task<Result<ProcessingResultWithQuality>> ProcessImage(
        byte[] imageData,
        ProcessingOptions processingOptions,
        Settings settings
    )
    {
        if (settings.Verbose)
        {
            AnsiConsole.MarkupLine($"[blue]Source image: {imageData.Length} bytes[/]");
        }

        return await AnsiConsole
            .Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Processing image[/]", maxValue: 100);

                task.Value = 10;
                task.Description = "[green]Initializing processing[/]";

                _logger.LogDebug(
                    "Starting face processing with options: {Options}",
                    processingOptions
                );

                task.Value = 20;

                // For quality assessment, use the quality extension method
                if (settings.QualityGate.HasValue || settings.GenerateQualityReport)
                {
                    task.Description = "[green]Processing with quality assessment[/]";
                    task.Value = 50;
                    var result = await ProcessWithQualityAssessment(
                        imageData,
                        processingOptions,
                        settings
                    );
                    task.Value = 100;
                    task.Description = "[green]Processing complete[/]";
                    return result;
                }

                // For standard processing, use the new biometric processing service
                task.Description = settings.NoResize
                    ? "[green]Encoding image without transformations[/]"
                    : "[green]Processing for PIV compliance[/]";
                task.Value = 50;

                var standardResult = settings.NoResize
                    ? await _processingService.ProcessWithEncodingOnlyAsync(
                        imageData,
                        processingOptions,
                        settings.Format,
                        settings.JpegQuality
                    )
                    : await _processingService.ProcessForPivAsync(
                        imageData,
                        processingOptions,
                        settings.Format
                    );

                task.Value = 100;
                task.Description = "[green]Processing complete[/]";

                return standardResult.Map(r => new ProcessingResultWithQuality(r, null));
            });
    }

    private async Task<Result<ProcessingResultWithQuality>> ProcessWithQualityAssessment(
        byte[] imageData,
        ProcessingOptions processingOptions,
        Settings settings
    )
    {
        _logger.LogDebug(
            "Performing quality assessment with gate: {QualityGate}, standard: {Standard}",
            settings.QualityGate,
            settings.QualityStandard
        );

        var qualityOptions = QualityAssessmentOptions.ForStandard(settings.QualityStandard) with
        {
            MinQualityThreshold = settings.QualityGate ?? 0.7f,
            EnforceCompliance = false, // Only enforce threshold, not compliance violations
        };

        return await imageData
            .ProcessWithQualityAsync(
                processingOptions,
                qualityOptions,
                settings.Format,
                settings.JpegQuality,
                _logger
            )
            .Bind(qualityResult =>
            {
                // Check quality gate if specified
                if (settings.QualityGate.HasValue)
                {
                    if (qualityResult.Quality.Overall.Value >= settings.QualityGate.Value)
                    {
                        AnsiConsole.MarkupLine("[green]Quality assessment passed[/]");
                        return Result.Success(
                            new ProcessingResultWithQuality(
                                qualityResult.Result,
                                qualityResult.Quality
                            )
                        );
                    }
                    else
                    {
                        AnsiConsole.MarkupLine(
                            $"[red]Quality assessment failed: score {qualityResult.Quality.Overall.Value:F2} below quality threshold {settings.QualityGate.Value:F2}[/]"
                        );
                        return Result.Failure<ProcessingResultWithQuality>(
                            $"Quality score {qualityResult.Quality.Overall.Value:F2} below quality threshold {settings.QualityGate.Value:F2}"
                        );
                    }
                }

                return Result.Success(
                    new ProcessingResultWithQuality(qualityResult.Result, qualityResult.Quality)
                );
            });
    }

    private async Task<Result> SaveResult(
        ProcessingResultDto processingResult,
        string outputPath,
        Settings settings,
        Iso19794Assessment? qualityAssessment
    )
    {
        return await Result.Try(
            async () =>
            {
                // Save the encoded image data
                await File.WriteAllBytesAsync(outputPath, processingResult.ImageData);

                // Display results
                await DisplayResults(processingResult, outputPath, settings, qualityAssessment);
            },
            ex => $"Failed to save result: {ex.Message}"
        );
    }

    /// <summary>
    /// Displays processing results to the console
    /// </summary>
    private async Task DisplayResults(
        ProcessingResultDto result,
        string outputPath,
        Settings settings,
        Iso19794Assessment? qualityAssessment
    )
    {
        AnsiConsole.WriteLine();

        // Show quality assessment status if quality gate was used
        if (settings.QualityGate.HasValue && qualityAssessment != null)
        {
            if (qualityAssessment.Overall.Value >= settings.QualityGate.Value)
            {
                AnsiConsole.MarkupLine(
                    $"[green]✓ Quality assessment passed (score: {qualityAssessment.Overall.Value:F2})[/]"
                );
            }
        }

        AnsiConsole.MarkupLine($"[green]Output saved to: {outputPath}[/]");

        // Show processing results
        var table = new Table().Border(TableBorder.Rounded).Title("[bold]Processing Results[/]");

        table.AddColumn("Property");
        table.AddColumn("Value");

        var metadata = result.Metadata;
        table.AddRow(
            "Output Dimensions",
            $"{metadata.OutputDimensions.Width}x{metadata.OutputDimensions.Height}"
        );
        table.AddRow("File Size", $"{metadata.FileSize:N0} bytes");
        table.AddRow("Face Confidence", $"{metadata.FaceConfidence:F2}");
        table.AddRow("Processing Time", $"{metadata.ProcessingTime.TotalMilliseconds:F0}ms");

        if (metadata.CompressionRate > 0)
            table.AddRow("Compression Rate", $"{metadata.CompressionRate:F2} bpp");

        if (metadata.TargetSize.HasValue)
            table.AddRow("Target Size", $"{metadata.TargetSize.Value:N0} bytes");

        AnsiConsole.Write(table);

        // Show quality assessment if available
        if (qualityAssessment != null)
        {
            AnsiConsole.WriteLine();

            // Show quality assessment header
            AnsiConsole.MarkupLine("[bold]Quality assessment[/]");

            // In verbose mode, show detailed scores
            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine($"Overall score: {qualityAssessment.Overall.Value:F2}");
                AnsiConsole.MarkupLine($"Symmetry: {qualityAssessment.Symmetry.Overall.Value:F2}");
                AnsiConsole.MarkupLine(
                    $"Sharpness: {qualityAssessment.Sharpness.Overall.Value:F2}"
                );
                AnsiConsole.MarkupLine($"Geometry: {qualityAssessment.Geometry.Overall.Value:F2}");
                AnsiConsole.WriteLine();
            }

            var qualityTable = new Table()
                .Border(TableBorder.Rounded)
                .Title(
                    $"[bold]Quality Assessment - {(qualityAssessment.IsCompliant ? "[green]COMPLIANT[/]" : "[red]NON-COMPLIANT[/]")}[/]"
                );
            qualityTable.AddColumn("Metric");
            qualityTable.AddColumn("Score");

            qualityTable.AddRow("Overall Quality", qualityAssessment.Overall.ToString());
            qualityTable.AddRow("├─ Symmetry", qualityAssessment.Symmetry.Overall.ToString());
            qualityTable.AddRow("├─ Sharpness", qualityAssessment.Sharpness.Overall.ToString());
            qualityTable.AddRow("└─ Geometry", qualityAssessment.Geometry.Overall.ToString());

            AnsiConsole.Write(qualityTable);

            if (qualityAssessment.Violations.Any())
            {
                AnsiConsole.MarkupLine("[yellow]Quality Violations:[/]");
                foreach (
                    var violation in qualityAssessment.Violations.OrderByDescending(v => v.Severity)
                )
                {
                    var color = violation.Severity switch
                    {
                        ViolationSeverity.Critical => "red",
                        ViolationSeverity.Moderate => "yellow",
                        _ => "gray",
                    };
                    AnsiConsole.MarkupLine(
                        $"  [{color}]• {violation.Category}: {violation.Description}[/]"
                    );
                }
            }

            // Save quality report if requested
            if (settings.GenerateQualityReport)
            {
                var reportPath = Path.ChangeExtension(outputPath, ".quality.json");
                var reportJson = JsonSerializer.Serialize(
                    new
                    {
                        qualityAssessment.IsCompliant,
                        OverallScore = qualityAssessment.Overall.Value,
                        Scores = new
                        {
                            Symmetry = qualityAssessment.Symmetry.Overall.Value,
                            Sharpness = qualityAssessment.Sharpness.Overall.Value,
                            Geometry = qualityAssessment.Geometry.Overall.Value,
                        },
                        qualityAssessment.Violations,
                        qualityAssessment.AssessmentTime,
                        ProcessingResult = new
                        {
                            metadata.OutputDimensions,
                            metadata.FileSize,
                            metadata.FaceConfidence,
                            metadata.ProcessingTime,
                        },
                    },
                    new JsonSerializerOptions { WriteIndented = true }
                );

                await File.WriteAllTextAsync(reportPath, reportJson);
                AnsiConsole.MarkupLine($"[blue]Quality report saved to: {reportPath}[/]");
            }
        }

        // Show verbose details
        if (settings.Verbose && metadata.AdditionalData.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[cyan]Additional Details:[/]");
            foreach (var kvp in metadata.AdditionalData)
            {
                var valueStr = kvp.Value?.ToString() ?? "null";
                AnsiConsole.MarkupLine(
                    $"[cyan]  {kvp.Key}: {valueStr.Replace("[", "[[").Replace("]", "]]")}[/]"
                );
            }
        }

        await Task.CompletedTask; // Make method async for consistency
    }

    /// <summary>
    /// Converts the input extension to the appropriate output format.
    /// </summary>
    /// <param name="inputPath"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    private static string GenerateOutputPath(string inputPath, string format = "jp2")
    {
        var directory = Path.GetDirectoryName(inputPath) ?? ".";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);

        var extension = format.ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => "jpg",
            "png" => "png",
            "tiff" or "tif" => "tiff",
            _ => "jp2",
        };

        return Path.Combine(directory, $"{nameWithoutExt}.{extension}");
    }

    /// <summary>
    /// Determines processing options based on command line settings
    /// </summary>
    private static ProcessingOptions DetermineProcessingOptions(
        Settings settings,
        ILogger<ProcessCommand> logger
    )
    {
        // Check for preset first
        if (!string.IsNullOrEmpty(settings.Preset))
        {
            var preset = GetPresetFromString(settings.Preset, logger);
            if (preset != null)
            {
                logger.LogDebug("Using preset: {Preset}", settings.Preset);
                return ApplyOverrides(preset, settings, logger);
            }
        }

        // Check for target size
        if (settings.TargetSize.HasValue)
        {
            logger.LogDebug("Using target size: {TargetSize} bytes", settings.TargetSize.Value);
            var options = ProcessingOptions.PivBalanced with
            {
                Strategy = EncodingStrategy.TargetSize(settings.TargetSize.Value),
            };
            return ApplyOverrides(options, settings, logger);
        }

        // Use custom rate if specified
        if (settings.BaseRate > 0)
        {
            logger.LogDebug("Using custom rate: {Rate} bpp", settings.BaseRate);
            var options = ProcessingOptions.PivBalanced with
            {
                Strategy = EncodingStrategy.FixedRate(settings.BaseRate),
            };
            return ApplyOverrides(options, settings, logger);
        }

        // Default to PIV standard
        logger.LogDebug("Using default PIV standard options");
        return ApplyOverrides(ProcessingOptions.PivBalanced, settings, logger);
    }

    /// <summary>
    /// Gets a processing preset from string name
    /// </summary>
    private static ProcessingOptions? GetPresetFromString(
        string presetName,
        ILogger<ProcessCommand> logger
    )
    {
        return presetName.ToLowerInvariant() switch
        {
            "twic-max" => ProcessingOptions.TwicMax,
            "piv-min" => ProcessingOptions.PivMin,
            "piv-balanced" => ProcessingOptions.PivBalanced,
            "piv-high" => ProcessingOptions.PivHigh,
            "piv-veryhigh" => ProcessingOptions.PivVeryHigh,
            "archival" => ProcessingOptions.Archival,
            "minimal" => ProcessingOptions.Minimal,
            "fast" => ProcessingOptions.Fast,
            _ => null,
        };
    }

    /// <summary>
    /// Applies CLI setting overrides to processing options
    /// </summary>
    private static ProcessingOptions ApplyOverrides(
        ProcessingOptions baseOptions,
        Settings settings,
        ILogger<ProcessCommand> logger
    )
    {
        var options = baseOptions;

        // Apply ROI level override
        if (settings.RoiStartLevel >= 0 && settings.RoiStartLevel <= 3)
        {
            if (settings.RoiStartLevel != baseOptions.RoiStartLevel)
            {
                logger.LogDebug(
                    "Overriding ROI level: {OldLevel} → {NewLevel}",
                    baseOptions.RoiStartLevel,
                    settings.RoiStartLevel
                );
                options = options with { RoiStartLevel = settings.RoiStartLevel };
            }
        }

        // Apply ROI enable/disable override
        if (settings.NoRoi && baseOptions.EnableRoi)
        {
            logger.LogDebug("Disabling ROI encoding");
            options = options with { EnableRoi = false };
        }

        // Apply ROI alignment override
        if (settings.Align && !baseOptions.AlignRoi)
        {
            logger.LogDebug("Enabling ROI alignment");
            options = options with { AlignRoi = true };
        }

        // Apply min confidence override
        if (
            settings.MinConfidence >= 0.0f
            && settings.MinConfidence <= 1.0f
            && Math.Abs(settings.MinConfidence - baseOptions.MinFaceConfidence) > 0.001f
        )
        {
            logger.LogDebug(
                "Overriding minimum face confidence: {OldConfidence} → {NewConfidence}",
                baseOptions.MinFaceConfidence,
                settings.MinConfidence
            );
            options = options with { MinFaceConfidence = settings.MinConfidence };
        }

        // Apply max rotation override
        if (
            settings.MaxRotation > 0.0f
            && Math.Abs(settings.MaxRotation - baseOptions.MaxRotationDegrees) > 0.001f
        )
        {
            logger.LogDebug(
                "Overriding maximum rotation: {OldRotation}° → {NewRotation}°",
                baseOptions.MaxRotationDegrees,
                settings.MaxRotation
            );
            options = options with { MaxRotationDegrees = settings.MaxRotation };
        }

        return options;
    }
}
