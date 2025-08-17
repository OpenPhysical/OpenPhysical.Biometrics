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
using OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;

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
            
            // Check if we have parsed biometric data
            if (payload.ParsedBiometricRecord.HasValue)
            {
                var record = payload.ParsedBiometricRecord.Value;
                
                // Extract image data based on record type
                if (record is Incits385BiometricRecord incits385)
                {
                    return await ExtractFacialImage(incits385.FacialRecord, settings, payload.Header);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Unsupported biometric record type:[/] {record.GetType().Name}");
                    return 1;
                }
            }
            else
            {
                // Fallback: Try to parse INCITS 385 manually if the format registry didn't work
                var header = payload.Header;
                if (header.BdbFormatOwner == 0x001B && header.BdbFormatType == 0x0501)
                {
                    // INCITS 385 Facial Image - parse manually
                    var parseResult = PivFacialFormat.ParseIncits385Record(payload.BiometricDataBlock);
                    if (parseResult.IsSuccess)
                    {
                        return await ExtractFacialImage(parseResult.Value, settings, header);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to parse INCITS 385 data:[/] {parseResult.Error}");
                        return 1;
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Unsupported biometric format:[/] Owner=0x{header.BdbFormatOwner:X4}, Type=0x{header.BdbFormatType:X4}");
                    return 1;
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task<int> ExtractFacialImage(
        FacialImageRecord facialRecord, 
        Settings settings,
        OpenPhysical.Domains.Cbeff.CbeffHeader header)
    {
        // Determine output file path
        var outputPath = settings.OutputPath;
        if (string.IsNullOrEmpty(outputPath))
        {
            var inputName = Path.GetFileNameWithoutExtension(settings.FilePath);
            var extension = facialRecord.ImageInfo.ImageDataType == 0 ? ".jpg" : ".jp2";
            outputPath = $"{inputName}_extracted{extension}";
        }

        // Write image data
        try
        {
            await File.WriteAllBytesAsync(outputPath, facialRecord.ImageData.ToArray());
            AnsiConsole.MarkupLine($"[green]Extracted image to:[/] {outputPath}");
            AnsiConsole.MarkupLine($"[dim]Format: {GetFormatName(facialRecord.ImageInfo.ImageDataType)}[/]");
            AnsiConsole.MarkupLine($"[dim]Dimensions: {facialRecord.ImageInfo.Width}×{facialRecord.ImageInfo.Height}[/]");
            AnsiConsole.MarkupLine($"[dim]Quality: {facialRecord.ImageInfo.Quality}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to write image file:[/] {ex.Message}");
            return 1;
        }

        // Save metadata if requested
        if (settings.SaveMetadata)
        {
            var metadataPath = Path.ChangeExtension(outputPath, ".json");
            var metadata = new
            {
                Source = settings.FilePath,
                ExtractedAt = DateTime.UtcNow,
                CbeffHeader = new
                {
                    CreationDate = header.CreationDate,
                    ValidityNotBefore = header.ValidityNotBefore,
                    ValidityNotAfter = header.ValidityNotAfter,
                    BiometricType = $"0x{header.BiometricType:X8}",
                    Quality = header.Quality,
                    Creator = GetCreatorString(header.Creator)
                },
                FacialRecord = new
                {
                    FormatIdentifier = facialRecord.Header.FormatIdentifier.TrimEnd('\0'),
                    VersionNumber = facialRecord.Header.VersionNumber.TrimEnd('\0'),
                    RecordLength = facialRecord.Header.RecordLength,
                    Gender = facialRecord.FacialInfo.Gender.ToString(),
                    EyeColor = GetEyeColorName(facialRecord.FacialInfo.EyeColor),
                    HairColor = GetHairColorName(facialRecord.FacialInfo.HairColor),
                    Expression = facialRecord.FacialInfo.Expression,
                    FeaturePointCount = facialRecord.FeaturePoints.Length
                },
                ImageInfo = new
                {
                    Type = GetImageTypeName(facialRecord.ImageInfo.FacialImageType),
                    Format = GetFormatName(facialRecord.ImageInfo.ImageDataType),
                    Width = facialRecord.ImageInfo.Width,
                    Height = facialRecord.ImageInfo.Height,
                    ColorSpace = GetColorSpaceName(facialRecord.ImageInfo.ImageColorSpace),
                    SourceType = GetSourceTypeName(facialRecord.ImageInfo.SourceType),
                    Quality = facialRecord.ImageInfo.Quality,
                    SizeBytes = facialRecord.ImageData.Length
                }
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

    private static string GetCreatorString(byte[] creator)
    {
        var str = System.Text.Encoding.ASCII.GetString(creator).TrimEnd('\0');
        return string.IsNullOrWhiteSpace(str) ? "<empty>" : str;
    }

    private static string GetFormatName(byte type) => type switch
    {
        0 => "JPEG",
        1 => "JPEG 2000",
        _ => $"Unknown ({type})"
    };

    private static string GetImageTypeName(byte type) => type switch
    {
        0x01 => "Full frontal",
        0x02 => "Token frontal",
        _ => $"Unknown ({type})"
    };

    private static string GetColorSpaceName(byte colorSpace) => colorSpace switch
    {
        0x00 => "Unspecified",
        0x01 => "24-bit RGB",
        0x02 => "YUV422",
        0x03 => "8-bit grayscale",
        0x04 => "Other",
        _ => $"Unknown ({colorSpace})"
    };

    private static string GetSourceTypeName(byte sourceType) => sourceType switch
    {
        0x00 => "Unspecified",
        0x01 => "Static photograph - unknown source",
        0x02 => "Static photograph - digital camera",
        0x03 => "Static photograph - scanner",
        0x04 => "Video frame - unknown source",
        0x05 => "Video frame - analog camera",
        0x06 => "Video frame - digital camera",
        0x07 => "Unknown",
        _ => $"Invalid ({sourceType})"
    };

    private static string GetEyeColorName(byte color) => color switch
    {
        0x00 => "Unspecified",
        0x01 => "Blue",
        0x02 => "Brown",
        0x03 => "Gray",
        0x04 => "Green",
        0x05 => "Hazel",
        0x06 => "Maroon",
        0x07 => "Pink",
        0x08 => "Multi-colored",
        0xFF => "Unknown",
        _ => $"Invalid ({color})"
    };

    private static string GetHairColorName(byte color) => color switch
    {
        0x00 => "Unspecified",
        0x01 => "Bald",
        0x02 => "Black",
        0x03 => "Blonde",
        0x04 => "Brown",
        0x05 => "Gray",
        0x06 => "Red",
        0x07 => "Blue",
        0x08 => "Green",
        0xFF => "Unknown",
        _ => $"Invalid ({color})"
    };
}