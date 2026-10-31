using System.Drawing;
using CoreJ2K.Skia;
using CoreJ2K.Util;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Piv;
using OpenPhysical.Biometrics.Tool.Drawing;

namespace OpenPhysical.Biometrics.Tool.Commands;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Standards.Incits385;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Cbeff;
using Spectre.Console;
using Spectre.Console.Cli;

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
            AnsiConsole.MarkupLine(
                $"[dim]Format: {(format == ImageFormat.Jpeg ? "JPEG" : "JPEG 2000")}[/]"
            );
            AnsiConsole.MarkupLine($"[dim]Dimensions: {dimensions.Width}×{dimensions.Height}[/]");

            // Parse metadata
            var metadata = ParseMetadata(settings);

            // Create INCITS 385 record
            var incitsWriter = _serviceProvider.GetRequiredService<IIncits385Writer>();
            var incitsResult = incitsWriter.CreateFacialRecord(
                imageData,
                format,
                dimensions,
                metadata
            );
            if (incitsResult.IsFailure)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Failed to create INCITS 385 record:[/] {incitsResult.Error}"
                );
                return 1;
            }

            // Apply the shared issuance profile to the complete encoded record.
            var validationResult = ValidateProfile(incitsResult.Value, settings.Standard);
            if (validationResult.IsFailure)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Image validation failed:[/] {Markup.Escape(validationResult.Error)}"
                );
                return 1;
            }

            AnsiConsole.MarkupLine("[green]Created INCITS 385 facial record[/]");

            // Create CBEFF container
            var cbeffBuilder = _serviceProvider.GetRequiredService<ICbeffBuilder>();
            var cbeffMetadata = CreateCbeffMetadata(settings);

            var formatResult = GetBiometricFormat(settings.Standard);
            if (formatResult.IsFailure)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Failed to get biometric format:[/] {formatResult.Error}"
                );
                return 1;
            }

            var cbeffResult = await cbeffBuilder.BuildAsync(
                incitsResult.Value,
                BiometricModality.Face,
                formatResult.Value,
                cbeffMetadata
            );

            if (cbeffResult.IsFailure)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Failed to create CBEFF container:[/] {cbeffResult.Error}"
                );
                return 1;
            }

            // Serialize to bytes
            var serializer = _serviceProvider.GetRequiredService<ICbeffSerializer>();
            var serializeResult = settings.PivWrap
                ? serializer.SerializeWithWrapper(cbeffResult.Value, 0xBC) // PIV BC tag
                : serializer.Serialize(cbeffResult.Value);

            if (serializeResult.IsFailure)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Failed to serialize CBEFF:[/] {serializeResult.Error}"
                );
                return 1;
            }

            // Write output file
            var outputPath =
                settings.OutputPath ?? Path.ChangeExtension(settings.ImagePath, ".cbeff");
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

    private Task<Result<(ImageFormat format, ImageDimensions dimensions)>> GetImageInfo(
        byte[] imageData
    )
    {
        try
        {
            var jpeg = imageData.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8 });
            if (jpeg)
            {
                using var image = RgbImage.Load(imageData);
                AnalyzeAndWarnAboutMetadata(imageData);
                return Task.FromResult(
                    Result.Success(
                        (ImageFormat.Jpeg, ImageDimensions.Create(image.Width, image.Height).Value)
                    )
                );
            }
            CoreJ2K.Util.ImageFactory.Register(new SKBitmapImageCreator());
            using var bitmap = CoreJ2K.J2kImage.DecodeToImage<SkiaSharp.SKBitmap>(imageData);
            return Task.FromResult(
                Result.Success(
                    (
                        ImageFormat.Jpeg2000,
                        ImageDimensions.Create(bitmap.Width, bitmap.Height).Value
                    )
                )
            );
        }
        catch (Exception error)
            when (error is ArgumentException or InvalidOperationException or IOException)
        {
            return Task.FromResult(
                Result.Failure<(ImageFormat, ImageDimensions)>(
                    "The encoded image could not be interpreted."
                )
            );
        }
    }

    private static Result<bool> ValidateProfile(byte[] encodedRecord, string standard)
    {
        if (standard.ToUpperInvariant() is not ("PIV" or "TWIC"))
            return Result.Failure<bool>($"Unknown standard: {standard}");
        var parsed = Incits385Codec.Parse(encodedRecord);
        if (!parsed.IsSuccess)
            return Result.Failure<bool>("The encoded facial record is malformed.");
        var diagnostics = PivBiometricProfile.Validate(parsed.Value!);
        return diagnostics.IsEmpty
            ? Result.Success(true)
            : Result.Failure<bool>(
                string.Join("; ", diagnostics.Select(d => $"{d.Code} ({d.Field})"))
            );
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
            FeaturePoints: Maybe<System.Collections.Immutable.ImmutableArray<FeaturePoint>>.None
        );
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
            ValidityEndDate: now.AddYears(10)
        ); // 10 year validity
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
                AnsiConsole.MarkupLine(
                    "[yellow]Warning: FASC-N should be 50 hex characters, using default[/]"
                );
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
            0x0501 // Face image
        );
    }

    private Gender ParseGender(string? input) =>
        input?.ToUpperInvariant() switch
        {
            "M" or "MALE" => Gender.Male,
            "F" or "FEMALE" => Gender.Female,
            "U" or "UNKNOWN" => Gender.Unknown,
            _ => Gender.Unspecified,
        };

    private EyeColor ParseEyeColor(string? input) =>
        input?.ToLowerInvariant() switch
        {
            "blue" => EyeColor.Blue,
            "brown" => EyeColor.Brown,
            "gray" or "grey" => EyeColor.Gray,
            "green" => EyeColor.Green,
            "hazel" => EyeColor.Hazel,
            "unknown" => EyeColor.Unknown,
            _ => EyeColor.Unspecified,
        };

    private HairColor ParseHairColor(string? input) =>
        input?.ToLowerInvariant() switch
        {
            "black" => HairColor.Black,
            "blonde" or "blond" => HairColor.Blonde,
            "brown" => HairColor.Brown,
            "gray" or "grey" => HairColor.Gray,
            "red" => HairColor.Red,
            "bald" => HairColor.Bald,
            "unknown" => HairColor.Unknown,
            _ => HairColor.Unspecified,
        };

    private void AnalyzeAndWarnAboutMetadata(byte[] imageData)
    {
        try
        {
            using var stream = new MemoryStream(imageData, writable: false);
            var fields = MetadataExtractor
                .ImageMetadataReader.ReadMetadata(stream)
                .Sum(directory => directory.Tags.Count);
            if (fields > 0)
                AnsiConsole.MarkupLine($"[dim]Image metadata: {fields} fields.[/]");
        }
        catch (MetadataExtractor.ImageProcessingException)
        {
            AnsiConsole.MarkupLine("[dim]Image metadata could not be interpreted.[/]");
        }
    }
}
