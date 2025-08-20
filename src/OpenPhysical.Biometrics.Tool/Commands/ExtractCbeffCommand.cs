namespace OpenPhysical.Biometrics.Tool.Commands;

using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Core.Domain;
using OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;

/// <summary>
/// Command to extract images from CBEFF containers.
/// </summary>
public sealed class ExtractCbeffCommand : AsyncCommand<ExtractCbeffCommand.Settings>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractCbeffCommand"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    public ExtractCbeffCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Settings for the extract CBEFF command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets the path to the CBEFF data file to extract from.
        /// </summary>
        [Description("Path to the CBEFF data file to extract from")]
        [CommandArgument(0, "<FILE>")]
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the output file path (defaults to auto-generated name).
        /// </summary>
        [Description("Output file path (defaults to auto-generated name)")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; init; }

        /// <summary>
        /// Gets a value indicating whether to also save metadata as JSON file.
        /// </summary>
        [Description("Also save metadata as JSON file")]
        [CommandOption("--save-metadata")]
        public bool SaveMetadata { get; init; }
    }

    /// <summary>
    /// Executes the extract CBEFF command asynchronously.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The command settings.</param>
    /// <returns>A task representing the asynchronous operation, with exit code 0 for success or 1 for failure.</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            if (!File.Exists(settings.FilePath))
            {
                AnsiConsole.MarkupLine($"[red]File not found:[/] {settings.FilePath}");
                return 1;
            }

            var cbeffData = await File.ReadAllBytesAsync(settings.FilePath);
            AnsiConsole.MarkupLine($"[green]Reading CBEFF file:[/] {settings.FilePath}");
            
            // Decode the CBEFF data
            var serializer = _serviceProvider.GetRequiredService<ICbeffSerializerExtended>();
            var decodeResult = serializer.DeserializeAutoExtended(cbeffData);

            if (decodeResult.IsFailure)
            {
                AnsiConsole.MarkupLine($"[red]Failed to decode CBEFF data:[/] {decodeResult.Error}");
                return 1;
            }

            var payload = decodeResult.Value;
            
            // Use the generic biometric format registry to process the data
            var registry = _serviceProvider.GetRequiredService<IBiometricFormatRegistry>();
            var header = payload.Header;
            
            // PIV uses patron format 0x03
            const byte PivPatronFormat = 0x03;
            
            var processingResult = registry.ProcessBiometricData(
                PivPatronFormat,
                header.BdbFormatOwner,
                header.BdbFormatType,
                payload.BiometricDataBlock);
                
            if (processingResult.IsFailure)
            {
                AnsiConsole.MarkupLine($"[red]Failed to process biometric data:[/] {processingResult.Error}");
                AnsiConsole.MarkupLine($"[dim]Format: Patron=0x{PivPatronFormat:X2}, Owner=0x{header.BdbFormatOwner:X4}, Type=0x{header.BdbFormatType:X4}[/]");
                return 1;
            }
            
            var extraction = processingResult.Value;
            
            // Extract the biometric data using the generic pipeline
            return await ExtractBiometricData(extraction, settings);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task<int> ExtractBiometricData(
        BiometricExtraction extraction,
        Settings settings)
    {
        var biometricData = extraction.Data;
        
        // Determine output file path
        var outputPath = settings.OutputPath;
        if (string.IsNullOrEmpty(outputPath))
        {
            var inputName = Path.GetFileNameWithoutExtension(settings.FilePath);
            outputPath = $"{inputName}_extracted{biometricData.SuggestedExtension}";
        }

        // Write extracted data
        try
        {
            await File.WriteAllBytesAsync(outputPath, biometricData.ExtractedData.ToArray());
            AnsiConsole.MarkupLine($"[green]Extracted {biometricData.ModalityType.ToLower()} data to:[/] {outputPath}");
            
            // Display modality-specific information
            DisplayExtractionSummary(biometricData);
            
            // Display validation results if any
            if (!extraction.IsValid && extraction.ValidationResults.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]Validation warnings:[/]");
                foreach (var validation in extraction.ValidationResults)
                {
                    AnsiConsole.MarkupLine($"[dim]  • {validation}[/]");
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to write extracted file:[/] {ex.Message}");
            return 1;
        }

        // Save metadata if requested
        if (settings.SaveMetadata)
        {
            var metadataPath = Path.ChangeExtension(outputPath, ".metadata.json");
            var metadata = new
            {
                Source = settings.FilePath,
                ExtractedAt = DateTime.UtcNow,
                SourceFormat = new
                {
                    FormatName = extraction.SourceFormat.FormatName,
                    PatronFormat = $"0x{extraction.SourceFormat.PatronFormat:X2}",
                    BdbFormatOwner = $"0x{extraction.SourceFormat.BdbFormatOwner:X4}",
                    BdbFormatType = $"0x{extraction.SourceFormat.BdbFormatType:X4}"
                },
                BiometricData = new
                {
                    ModalityType = biometricData.ModalityType,
                    FormatIdentifier = biometricData.FormatIdentifier,
                    SuggestedExtension = biometricData.SuggestedExtension,
                    Metadata = biometricData.Metadata
                },
                AnalysisResults = extraction.AnalysisResults,
                ValidationResults = extraction.ValidationResults,
                IsValid = extraction.IsValid
            };

            try
            {
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(metadataPath, json);
                AnsiConsole.MarkupLine($"[green]Saved metadata to:[/] {metadataPath}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Failed to save metadata:[/] {ex.Message}");
            }
        }

        return 0;
    }

    private static void DisplayExtractionSummary(ExtractedBiometricData biometricData)
    {
        switch (biometricData)
        {
            case FacialImageData facialData:
                AnsiConsole.MarkupLine($"[dim]Format: {GetImageFormatName(facialData.SuggestedExtension)}[/]");
                AnsiConsole.MarkupLine($"[dim]Dimensions: {facialData.Width}×{facialData.Height}[/]");
                AnsiConsole.MarkupLine($"[dim]Quality: {facialData.Quality}[/]");
                break;
                
            case FingerprintMinutiaeData fingerprintData:
                AnsiConsole.MarkupLine($"[dim]Total minutiae: {fingerprintData.TotalMinutiae}[/]");
                AnsiConsole.MarkupLine($"[dim]Finger views: {fingerprintData.NumberOfViews}[/]");
                AnsiConsole.MarkupLine($"[dim]Empty template: {fingerprintData.IsEmptyTemplate}[/]");
                break;
                
            default:
                AnsiConsole.MarkupLine($"[dim]Format: {biometricData.FormatIdentifier}[/]");
                AnsiConsole.MarkupLine($"[dim]Size: {biometricData.ExtractedData.Length} bytes[/]");
                break;
        }
    }

    private static string GetImageFormatName(string extension) => extension switch
    {
        ".jpg" => "JPEG",
        ".jp2" => "JPEG 2000",
        ".json" => "JSON (Minutiae)",
        ".bin" => "Binary",
        _ => "Unknown"
    };

}