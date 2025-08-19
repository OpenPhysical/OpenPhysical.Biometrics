using System.ComponentModel;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Domain.Common;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Infrastructure.Services;
using OpenPhysical.Biometrics.Infrastructure.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenPhysical.Biometrics.Tool.Commands;

/// <summary>
/// Simplified process command that delegates to library services.
/// </summary>
[Description("Process image using library services (v2)")]
[UsedImplicitly]
public sealed class ProcessCommandV2 : AsyncCommand<ProcessCommandV2.Settings>
{
    /// <summary>
    /// Command settings.
    /// </summary>
    [UsedImplicitly]
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Input file path.
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
        /// Processing preset.
        /// </summary>
        [CommandOption("-p|--preset <PRESET>")]
        [Description("Processing preset: piv-balanced, twic-max, piv-high")]
        [DefaultValue("piv-balanced")]
        public string Preset { get; set; } = "piv-balanced";

        /// <summary>
        /// Target file size in bytes.
        /// </summary>
        [CommandOption("--target-size <SIZE>")]
        [Description("Target file size in bytes (overrides preset)")]
        public int? TargetSize { get; set; }

        /// <summary>
        /// Enable quality assessment.
        /// </summary>
        [CommandOption("-q|--quality")]
        [Description("Enable quality assessment")]
        public bool EnableQuality { get; set; }

        /// <summary>
        /// Show detailed information.
        /// </summary>
        [CommandOption("--verbose")]
        [Description("Show detailed information")]
        public bool Verbose { get; set; }
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

            // Create service provider
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                if (settings.Verbose)
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
                }
            });
            services.AddBiometricServices();

            using var serviceProvider = services.BuildServiceProvider();
            
            // Get services
            var imageService = serviceProvider.GetRequiredService<IImageProcessingService>();
            var jpeg2000Service = serviceProvider.GetRequiredService<IJpeg2000Service>();
            var pipelineService = serviceProvider.GetRequiredService<IBiometricPipelineService>();
            var logger = serviceProvider.GetRequiredService<ILogger<ProcessCommandV2>>();

            // Determine output path
            var outputPath = settings.OutputPath ?? GenerateOutputPath(settings.InputPath);

            // Load image
            var imageData = ImmutableArray.Create(await File.ReadAllBytesAsync(settings.InputPath));

            await AnsiConsole.Status()
                .StartAsync("Processing image...", async ctx =>
                {
                    // Step 1: Run minimal pipeline for face detection and transformation
                    ctx.Status("Detecting faces and transforming image...");
                    
                    var pipelineBuilder = pipelineService.CreatePipelineBuilder()
                        .AddStep("face-detection")
                        .AddStep("landmark-extraction")
                        .AddStep("image-transformation");

                    if (settings.EnableQuality)
                    {
                        pipelineBuilder.AddStep("quality-assessment");
                    }

                    var pipeline = pipelineBuilder.Build();
                    var executionPolicy = PipelineExecutionPolicy.Automated;
                    var metadata = ImmutableDictionary<string, object>.Empty;

                    var pipelineResult = await pipelineService.ExecuteCustomPipelineAsync(
                        imageData, pipeline, executionPolicy, metadata);

                    if (pipelineResult.IsFailure)
                    {
                        throw new InvalidOperationException($"Pipeline failed: {pipelineResult.Error}");
                    }

                    // Step 2: Get transformed image from pipeline result
                    var processedData = pipelineResult.Value.FinalData;
                    var transformedImageData = processedData.Data.RawData;

                    // Step 3: Encode with JPEG 2000
                    ctx.Status("Encoding to JPEG 2000...");

                    Result<EncodedJpeg2000> encodingResult;
                    
                    if (settings.TargetSize.HasValue)
                    {
                        // Use target size encoding
                        var targetOptions = new TargetSizeOptions();
                        var roiSet = ExtractRoiFromMetadata(processedData.ProcessingMetadata);
                        
                        encodingResult = await jpeg2000Service.EncodeToTargetSizeAsync(
                            transformedImageData,
                            settings.TargetSize.Value,
                            roiSet,
                            targetOptions);
                    }
                    else
                    {
                        // Use preset encoding
                        var options = GetJpeg2000Options(settings.Preset);
                        
                        if (processedData.ProcessingMetadata.TryGetValue("roi_set", out var roiData) &&
                            roiData is FacialRoiSet roiSet)
                        {
                            encodingResult = await jpeg2000Service.EncodeWithRoiAsync(
                                transformedImageData, roiSet, options);
                        }
                        else
                        {
                            encodingResult = await jpeg2000Service.EncodeAsync(
                                transformedImageData, options);
                        }
                    }

                    if (encodingResult.IsFailure)
                    {
                        throw new InvalidOperationException($"Encoding failed: {encodingResult.Error}");
                    }

                    // Step 4: Save output
                    ctx.Status("Saving output...");
                    var encoded = encodingResult.Value;
                    await File.WriteAllBytesAsync(outputPath, encoded.ImageData.ToArray());

                    // Show results
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[green]✓ Processing completed successfully![/]");
                    AnsiConsole.MarkupLine($"[green]Output saved to: {outputPath}[/]");
                    
                    var resultTable = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("Property")
                        .AddColumn("Value");

                    resultTable.AddRow("Dimensions", $"{encoded.Dimensions.Width}x{encoded.Dimensions.Height}");
                    resultTable.AddRow("File Size", $"{encoded.FileSize:N0} bytes");
                    resultTable.AddRow("Compression Rate", $"{encoded.CompressionRate:F2} bpp");
                    
                    if (settings.EnableQuality && processedData.Quality.OverallQuality.Value > 0)
                    {
                        resultTable.AddRow("Quality Score", processedData.Quality.OverallQuality.ToString());
                    }

                    AnsiConsole.Write(resultTable);
                });

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            if (ex.InnerException != null)
            {
                AnsiConsole.MarkupLine($"[red]Details: {ex.InnerException.Message}[/]");
            }
            return 1;
        }
    }

    private static Infrastructure.Services.Jpeg2000Options GetJpeg2000Options(string preset)
    {
        return preset.ToLowerInvariant() switch
        {
            "piv-balanced" => new Infrastructure.Services.Jpeg2000Options(CompressionRate: 0.7f),
            "piv-high" => new Infrastructure.Services.Jpeg2000Options(CompressionRate: 1.0f),
            "twic-max" => new Infrastructure.Services.Jpeg2000Options(CompressionRate: 0.5f),
            _ => new Infrastructure.Services.Jpeg2000Options()
        };
    }

    private static Maybe<FacialRoiSet> ExtractRoiFromMetadata(ImmutableDictionary<string, object> metadata)
    {
        if (metadata.TryGetValue("roi_set", out var roiData) && roiData is FacialRoiSet roiSet)
        {
            return Maybe<FacialRoiSet>.From(roiSet);
        }
        return Maybe<FacialRoiSet>.None;
    }

    private static string GenerateOutputPath(string inputPath)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? ".";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory, $"{nameWithoutExt}.jp2");
    }
}