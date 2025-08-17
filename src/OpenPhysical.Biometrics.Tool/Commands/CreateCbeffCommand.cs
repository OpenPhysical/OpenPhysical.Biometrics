namespace OpenPhysical.Biometrics.Tool.Commands;

using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Standards.Incits385;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Cbeff;
using SixLabors.ImageSharp;
using System.Text;
using System.Globalization;
using CSharpFunctionalExtensions;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Command to create CBEFF containers from facial images.
/// </summary>
public sealed class CreateCbeffCommand : AsyncCommand<CreateCbeffCommand.Settings>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCbeffCommand"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    public CreateCbeffCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Settings for the create CBEFF command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets the path to the facial image file (JPEG or JPEG 2000).
        /// </summary>
        [Description("Path to the facial image file (JPEG or JPEG 2000)")]
        [CommandArgument(0, "<IMAGE>")]
        public string ImagePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the output CBEFF file path.
        /// </summary>
        [Description("Output CBEFF file path")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; init; }

        /// <summary>
        /// Gets the standard to use (PIV or TWIC).
        /// </summary>
        [Description("Standard to use (PIV or TWIC)")]
        [CommandOption("--standard")]
        public string Standard { get; init; } = "PIV";

        /// <summary>
        /// Gets the quality score 0-100 (defaults to 0 for INCITS 385-2004 compliance).
        /// </summary>
        [Description("Quality score 0-100 (defaults to 0 for INCITS 385-2004 compliance)")]
        [CommandOption("--quality")]
        public int? Quality { get; init; }

        /// <summary>
        /// Gets the FASC-N as hex string (50 hex characters).
        /// </summary>
        [Description("FASC-N as hex string (50 hex characters)")]
        [CommandOption("--fascn")]
        public string? FascN { get; init; }

        /// <summary>
        /// Gets the gender (M/F/U).
        /// </summary>
        [Description("Gender (M/F/U)")]
        [CommandOption("--gender")]
        public string? Gender { get; init; }

        /// <summary>
        /// Gets the eye color (blue/brown/gray/green/hazel/unknown).
        /// </summary>
        [Description("Eye color (blue/brown/gray/green/hazel/unknown)")]
        [CommandOption("--eye-color")]
        public string? EyeColor { get; init; }

        /// <summary>
        /// Gets the hair color (black/blonde/brown/gray/red/bald/unknown).
        /// </summary>
        [Description("Hair color (black/blonde/brown/gray/red/bald/unknown)")]
        [CommandOption("--hair-color")]
        public string? HairColor { get; init; }

        /// <summary>
        /// Gets the creator name (max 18 characters).
        /// </summary>
        [Description("Creator name (max 18 characters)")]
        [CommandOption("--creator")]
        public string Creator { get; init; } = "OpenPhysical";

        /// <summary>
        /// Gets a value indicating whether to wrap with PIV BC tag for card storage.
        /// </summary>
        [Description("Wrap with PIV BC tag for card storage")]
        [CommandOption("--piv-wrap")]
        public bool PivWrap { get; init; }
    }

    /// <summary>
    /// Executes the create CBEFF command asynchronously.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The command settings.</param>
    /// <returns>A task representing the asynchronous operation, with exit code 0 for success or 1 for failure.</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            if (!File.Exists(settings.ImagePath))
            {
                AnsiConsole.MarkupLine($"[red]Image file not found:[/] {settings.ImagePath}");
                return 1;
            }

            // Read and validate image
            var imageData = await File.ReadAllBytesAsync(settings.ImagePath);
            var imageInfoResult = await GetImageInfo(imageData);
            if (imageInfoResult.IsFailure)
            {
                AnsiConsole.MarkupLine($"[red]Failed to read image:[/] {imageInfoResult.Error}");
                return 1;
            }

            var (format, dimensions) = imageInfoResult.Value;
            
            AnsiConsole.MarkupLine($"[green]Processing image:[/] {settings.ImagePath}");
            AnsiConsole.MarkupLine($"[dim]Format: {(format == ImageFormat.Jpeg ? "JPEG" : "JPEG 2000")}[/]");
            AnsiConsole.MarkupLine($"[dim]Dimensions: {dimensions.Width}×{dimensions.Height}[/]");

            // Validate dimensions for standard
            var validationResult = ValidateDimensions(dimensions, settings.Standard);
            if (validationResult.IsFailure)
            {
                AnsiConsole.MarkupLine($"[red]Image validation failed:[/] {validationResult.Error}");
                return 1;
            }

            // Parse metadata
            var metadata = ParseMetadata(settings);

            // Create INCITS 385 record
            var incitsWriter = _serviceProvider.GetRequiredService<IIncits385Writer>();
            var incitsResult = incitsWriter.CreateFacialRecord(imageData, format, dimensions, metadata);
            if (incitsResult.IsFailure)
            {
                AnsiConsole.MarkupLine($"[red]Failed to create INCITS 385 record:[/] {incitsResult.Error}");
                return 1;
            }

            AnsiConsole.MarkupLine("[green]Created INCITS 385 facial record[/]");

            // Create CBEFF container
            var cbeffBuilder = _serviceProvider.GetRequiredService<ICbeffBuilder>();
            var cbeffMetadata = CreateCbeffMetadata(settings);
            
            var formatResult = GetBiometricFormat(settings.Standard);
            if (formatResult.IsFailure)
            {
                AnsiConsole.MarkupLine($"[red]Failed to get biometric format:[/] {formatResult.Error}");
                return 1;
            }
            
            var cbeffResult = await cbeffBuilder.BuildAsync(
                incitsResult.Value,
                BiometricModality.Face,
                formatResult.Value,
                cbeffMetadata);

            if (cbeffResult.IsFailure)
            {
                AnsiConsole.MarkupLine($"[red]Failed to create CBEFF container:[/] {cbeffResult.Error}");
                return 1;
            }

            // Serialize to bytes
            var serializer = _serviceProvider.GetRequiredService<ICbeffSerializer>();
            var serializeResult = settings.PivWrap
                ? serializer.SerializeWithWrapper(cbeffResult.Value, 0xBC) // PIV BC tag
                : serializer.Serialize(cbeffResult.Value);
                
            if (serializeResult.IsFailure)
            {
                AnsiConsole.MarkupLine($"[red]Failed to serialize CBEFF:[/] {serializeResult.Error}");
                return 1;
            }

            // Write output file
            var outputPath = settings.OutputPath ?? Path.ChangeExtension(settings.ImagePath, ".cbeff");
            await File.WriteAllBytesAsync(outputPath, serializeResult.Value.ToArray());

            AnsiConsole.MarkupLine($"[green]Created CBEFF file:[/] {outputPath}");
            AnsiConsole.MarkupLine($"[dim]Size: {serializeResult.Value.Length:N0} bytes[/]");
            if (settings.PivWrap)
            {
                AnsiConsole.MarkupLine($"[dim]Format: PIV-wrapped (BC tag)[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private Task<Result<(ImageFormat format, ImageDimensions dimensions)>> GetImageInfo(byte[] imageData)
    {
        try
        {
            // Check for JPEG
            if (imageData.Length >= 2 && imageData[0] == 0xFF && imageData[1] == 0xD8)
            {
                using var image = Image.Load(imageData);
                var dimensionsResult = ImageDimensions.Create(image.Width, image.Height);
                
                // Analyze metadata and warn if significant
                AnalyzeAndWarnAboutMetadata(image);
                
                var result = dimensionsResult.IsFailure 
                    ? Result.Failure<(ImageFormat, ImageDimensions)>(dimensionsResult.Error)
                    : Result.Success((ImageFormat.Jpeg, dimensionsResult.Value));
                return Task.FromResult(result);
            }

            // Check for JPEG 2000 (JP2)
            if (imageData.Length >= 12)
            {
                var jp2Signature = new byte[] { 0x00, 0x00, 0x00, 0x0c, 0x6a, 0x50, 0x20, 0x20, 0x0d, 0x0a, 0x87, 0x0a };
                if (imageData.Take(12).SequenceEqual(jp2Signature))
                {
                    // For JPEG 2000, we'd need a proper parser. For now, use a placeholder.
                    // In production, you'd parse the JP2 header to get dimensions.
                    return Task.FromResult(Result.Failure<(ImageFormat, ImageDimensions)>("JPEG 2000 dimension parsing not yet implemented"));
                }
            }

            // Check for JPEG 2000 codestream (J2C)
            if (imageData.Length >= 4 && imageData[0] == 0xFF && imageData[1] == 0x4F && 
                imageData[2] == 0xFF && imageData[3] == 0x51)
            {
                return Task.FromResult(Result.Failure<(ImageFormat, ImageDimensions)>("JPEG 2000 codestream dimension parsing not yet implemented"));
            }

            return Task.FromResult(Result.Failure<(ImageFormat, ImageDimensions)>("Unknown image format"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<(ImageFormat, ImageDimensions)>($"Error reading image: {ex.Message}"));
        }
    }

    private Result<bool> ValidateDimensions(ImageDimensions dimensions, string standard)
    {
        return standard.ToUpperInvariant() switch
        {
            "PIV" => dimensions.Width >= 88 && dimensions.Width <= 1024 &&
                     dimensions.Height >= 108 && dimensions.Height <= 1280
                     ? Result.Success(true)
                     : Result.Failure<bool>($"PIV requires dimensions between 88×108 and 1024×1280, got {dimensions.Width}×{dimensions.Height}"),
            
            "TWIC" => dimensions.Width >= 88 && dimensions.Width <= 1024 &&
                      dimensions.Height >= 108 && dimensions.Height <= 1280
                      ? Result.Success(true)
                      : Result.Failure<bool>($"TWIC requires dimensions between 88×108 and 1024×1280, got {dimensions.Width}×{dimensions.Height}"),
            
            _ => Result.Failure<bool>($"Unknown standard: {standard}")
        };
    }

    private Incits385Metadata ParseMetadata(Settings settings)
    {
        var quality = CalculateQualityValue(settings);
        var gender = ParseGender(settings.Gender);
        var eyeColor = ParseEyeColor(settings.EyeColor);
        var hairColor = ParseHairColor(settings.HairColor);

        return new Incits385Metadata(
            Quality: quality,
            Gender: gender,
            EyeColor: eyeColor,
            HairColor: hairColor,
            FeaturePoints: Maybe<System.Collections.Immutable.ImmutableArray<FeaturePoint>>.None);
    }

    private CbeffMetadata CreateCbeffMetadata(Settings settings)
    {
        var fascN = ParseFascN(settings.FascN);
        var now = DateTime.UtcNow;
        var quality = CalculateQualityValue(settings);

        return new CbeffMetadata(
            PatronHeaderVersion: 0x03, // PIV patron format
            SecurityOptions: 0x00, // No security
            BiometricQuality: (sbyte)quality, // Use same quality calculation as INCITS 385 record
            CreatorName: settings.Creator,
            FascN: fascN,
            CreationDate: now,
            ValidityStartDate: now,
            ValidityEndDate: now.AddYears(10)); // 10 year validity
    }

    private byte CalculateQualityValue(Settings settings)
    {
        return settings.Quality.HasValue
            ? (byte)Math.Min(100, Math.Max(0, settings.Quality.Value))
            : (byte)0; // INCITS 385-2004 requires 0 (unspecified), SP 800-76-2 permits 0-100
    }

    private byte[] ParseFascN(string? fascNHex)
    {
        if (string.IsNullOrEmpty(fascNHex))
        {
            // Return default FASC-N (all zeros)
            return new byte[25];
        }

        try
        {
            // Remove any spaces or dashes
            var cleaned = fascNHex.Replace(" ", "").Replace("-", "");
            
            if (cleaned.Length != 50) // 25 bytes = 50 hex chars
            {
                AnsiConsole.MarkupLine("[yellow]Warning: FASC-N should be 50 hex characters, using default[/]");
                return new byte[25];
            }

            var bytes = new byte[25];
            for (int i = 0; i < 25; i++)
            {
                bytes[i] = byte.Parse(cleaned.Substring(i * 2, 2), NumberStyles.HexNumber);
            }
            return bytes;
        }
        catch
        {
            AnsiConsole.MarkupLine("[yellow]Warning: Invalid FASC-N format, using default[/]");
            return new byte[25];
        }
    }

    private Result<BiometricFormat> GetBiometricFormat(string standard)
    {
        // Both PIV and TWIC use INCITS 385 format for facial images
        return BiometricFormat.Create(
            "2.16.840.1.101.3.7.2.16.3",
            "INCITS 385 Face Image",
            0x001B, // INCITS M1
            0x0501  // Face image
        );
    }

    private Gender ParseGender(string? input) => input?.ToUpperInvariant() switch
    {
        "M" or "MALE" => Gender.Male,
        "F" or "FEMALE" => Gender.Female,
        "U" or "UNKNOWN" => Gender.Unknown,
        _ => Gender.Unspecified
    };

    private EyeColor ParseEyeColor(string? input) => input?.ToLowerInvariant() switch
    {
        "blue" => EyeColor.Blue,
        "brown" => EyeColor.Brown,
        "gray" or "grey" => EyeColor.Gray,
        "green" => EyeColor.Green,
        "hazel" => EyeColor.Hazel,
        "unknown" => EyeColor.Unknown,
        _ => EyeColor.Unspecified
    };

    private HairColor ParseHairColor(string? input) => input?.ToLowerInvariant() switch
    {
        "black" => HairColor.Black,
        "blonde" or "blond" => HairColor.Blonde,
        "brown" => HairColor.Brown,
        "gray" or "grey" => HairColor.Gray,
        "red" => HairColor.Red,
        "bald" => HairColor.Bald,
        "unknown" => HairColor.Unknown,
        _ => HairColor.Unspecified
    };

    private void AnalyzeAndWarnAboutMetadata(Image image)
    {
        var metadataTypes = new List<string>();
        var estimatedSize = 0;

        // Check EXIF data
        if (image.Metadata.ExifProfile?.Values.Count > 0)
        {
            metadataTypes.Add("EXIF");
            estimatedSize += EstimateExifSize(image.Metadata.ExifProfile);
        }

        // Check XMP data
        if (image.Metadata.XmpProfile != null)
        {
            metadataTypes.Add("XMP");
            estimatedSize += image.Metadata.XmpProfile.ToByteArray().Length;
        }

        // Check IPTC data
        var iptcProfile = image.Metadata.IptcProfile;
        if (iptcProfile != null)
        {
            var iptcCount = GetIptcValueCount(iptcProfile);
            if (iptcCount > 0)
            {
                metadataTypes.Add("IPTC");
                estimatedSize += iptcCount * 30; // Estimate 30 bytes per IPTC value
            }
        }

        // Check for embedded thumbnail
        if (image.Metadata.ExifProfile?.Values?.Any(v => v.Tag.ToString().Contains("Thumbnail")) == true)
        {
            metadataTypes.Add("Thumbnail");
            estimatedSize += 8000; // Typical thumbnail size estimate
        }

        // Only warn if metadata is significant (> 1KB)
        if (estimatedSize > 1024)
        {
            var typesString = string.Join(", ", metadataTypes);
            AnsiConsole.MarkupLine($"[yellow]⚠ Warning:[/] Image contains {estimatedSize:N0} bytes of metadata ({typesString})");
            AnsiConsole.MarkupLine("[dim]This metadata will be preserved in the CBEFF but may not be needed for biometric applications.[/]");
            AnsiConsole.MarkupLine("[dim]Consider using a stripped image to reduce CBEFF size.[/]");
            AnsiConsole.WriteLine();
        }
    }

    private int EstimateExifSize(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile exifProfile)
    {
        // Rough estimate: each EXIF value averages about 20 bytes (tag + type + length + data)
        var count = exifProfile.Values?.Count ?? 0;
        return count * 20;
    }

    private int GetIptcValueCount(SixLabors.ImageSharp.Metadata.Profiles.Iptc.IptcProfile iptcProfile)
    {
        try
        {
            // Try to access Values collection
            return iptcProfile.Values?.Count() ?? 0;
        }
        catch
        {
            // If there's any issue accessing the collection, assume no values
            return 0;
        }
    }
}