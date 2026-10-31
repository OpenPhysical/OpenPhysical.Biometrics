using System.Collections.Immutable;
using System.ComponentModel;
using System.Drawing;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Common;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Biometrics.Core.Domain.Quality.Sharpness;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Infrastructure.Extensions;
using OpenPhysical.Biometrics.Infrastructure.Services;
using OpenPhysical.Biometrics.Tool.Drawing;
using Spectre.Console;
using Spectre.Console.Cli;
using OverlayColor = System.Drawing.Color;

namespace OpenPhysical.Biometrics.Tool.Commands;

/// <summary>
/// Quick compliance validation command - returns pass/fail without processing
/// </summary>
[Description("Quick compliance validation - pass/fail without processing")]
public sealed class ValidateCommand(
    IFaceDetector faceDetector,
    ILandmarkExtractor landmarkExtractor,
    ILogger<ValidateCommand> logger
) : AsyncCommand<ValidateCommand.Settings>
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
        /// Compliance standard to validate against
        /// </summary>
        [CommandOption("-s|--standard <STANDARD>")]
        [Description("Compliance standard: piv, twic, icao, cac")]
        [DefaultValue("piv")]
        public string Standard { get; set; } = "piv";

        /// <summary>
        /// Validation mode: input (photographer validation) or output (credential compliance)
        /// </summary>
        [CommandOption("-m|--mode <MODE>")]
        [Description(
            "Validation mode: input (photographer validation) or output (credential compliance)"
        )]
        [DefaultValue("input")]
        public string Mode { get; set; } = "input";

        /// <summary>
        /// Show detailed violation reasons
        /// </summary>
        [CommandOption("--detailed")]
        [Description("Show detailed violation reasons")]
        public bool ShowDetailed { get; set; }

        /// <summary>
        /// Minimum face detection confidence
        /// </summary>
        [CommandOption("--min-confidence <CONFIDENCE>")]
        [Description("Minimum face detection confidence (0.0 to 1.0)")]
        [DefaultValue(0.8f)]
        public float MinConfidence { get; set; } = 0.8f;

        /// <summary>
        /// Enable debug logging
        /// </summary>
        [CommandOption("--debug")]
        [Description("Enable debug logging for troubleshooting")]
        public bool Debug { get; set; }

        /// <summary>
        /// Save visualization of face ROI and analysis areas
        /// </summary>
        [CommandOption("--visualize")]
        [Description("Save visualization of face ROI and analysis areas")]
        public bool Visualize { get; set; }

        /// <summary>
        /// Output path for visualization (if not specified, uses input name + _analysis)
        /// </summary>
        [CommandOption("--viz-output <PATH>")]
        [Description("Output path for visualization image")]
        public string? VizOutput { get; set; }

        /// <summary>
        /// Show sharpness analysis details
        /// </summary>
        [CommandOption("--sharpness-details")]
        [Description("Show detailed sharpness analysis information")]
        public bool SharpnessDetails { get; set; }
    }

    private readonly IFaceDetector _faceDetector =
        faceDetector ?? throw new ArgumentNullException(nameof(faceDetector));
    private readonly ILandmarkExtractor _landmarkExtractor =
        landmarkExtractor ?? throw new ArgumentNullException(nameof(landmarkExtractor));
    private readonly ILogger<ValidateCommand> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            // Validate input
            if (!File.Exists(settings.InputPath))
            {
                _logger.LogError(new EventId(844636, "ValidateCommand"), "ValidateCommand failed.");
                AnsiConsole.MarkupLine(
                    $"[red]Error: Input file '{settings.InputPath}' not found.[/]"
                );
                return 1;
            }

            // Validate standard
            if (!ComplianceStandards.IsSupported(settings.Standard))
            {
                var supportedStandards = string.Join(
                    ", ",
                    ComplianceStandards.GetSupportedStandards()
                );
                _logger.LogError(new EventId(858538, "ValidateCommand"), "ValidateCommand failed.");
                AnsiConsole.MarkupLine(
                    $"[red]Error: Unsupported standard '{settings.Standard}'. Supported: {supportedStandards}[/]"
                );
                return 1;
            }

            // Validate mode
            if (!IsValidMode(settings.Mode))
            {
                _logger.LogError(new EventId(827600, "ValidateCommand"), "ValidateCommand failed.");
                AnsiConsole.MarkupLine(
                    $"[red]Error: Invalid mode '{settings.Mode}'. Supported: input, output[/]"
                );
                return 1;
            }

            var success = false;
            ComplianceAssessment? assessment = null;
            DetectedFace? detectedFace = null;
            FaceLandmarks68? landmarks = null;
            RgbImage? image = null;
            RgbImage? alignedImage = null;

            if (settings.Debug)
            {
                // Debug mode without spinner
                AnsiConsole.MarkupLine("[grey]Loading image...[/]");
                var imageData = await File.ReadAllBytesAsync(settings.InputPath);
                image = RgbImage.Load(imageData);

                // Process with face alignment for visualization
                if (settings.Visualize || settings.SharpnessDetails)
                {
                    AnsiConsole.MarkupLine("[grey]Detecting and aligning face...[/]");
                    var faceResult = await _faceDetector.DetectFacesAsync(image);
                    if (faceResult.IsSuccess && faceResult.Value.Any())
                    {
                        detectedFace = faceResult
                            .Value.OrderByDescending(f => f.BoundingBox.Area)
                            .First();

                        AnsiConsole.MarkupLine("[grey]Processing with PIV alignment...[/]");
                        var processingOptions = PivProcessingOptions.Default;
                        var alignedResult = await PivLandmarkProcessor.ProcessAsync(
                            image,
                            detectedFace,
                            _landmarkExtractor,
                            processingOptions,
                            _logger
                        );

                        if (alignedResult.IsSuccess)
                        {
                            var pivResult = alignedResult.Value;
                            alignedImage = pivResult.PivImage.Clone(); // Clone for our use
                            landmarks = pivResult.Landmarks;
                            _logger.LogDebug(
                                new EventId(827334, "ValidateCommand"),
                                "ValidateCommand stage completed."
                            );
                        }
                        else
                        {
                            _logger.LogWarning(
                                new EventId(827269, "ValidateCommand"),
                                "ValidateCommand requires review."
                            );
                            // Fallback to original landmarks
                            var landmarkResult = await _landmarkExtractor.ExtractLandmarksAsync(
                                image,
                                detectedFace.BoundingBox
                            );
                            if (landmarkResult.IsSuccess)
                            {
                                landmarks = landmarkResult.Value;
                            }
                        }
                    }
                }

                AnsiConsole.MarkupLine(
                    $"[grey]Validating {settings.Standard.ToUpperInvariant()} compliance...[/]"
                );

                try
                {
                    var assessmentMode = ParseAssessmentMode(settings.Mode);
                    var result = await imageData.ValidateComplianceAsync(
                        settings.Standard,
                        settings.MinConfidence,
                        _logger,
                        assessmentMode
                    );
                    if (result.IsSuccess)
                    {
                        assessment = result.Value;
                        success = true;
                    }
                    else
                    {
                        _logger.LogError(
                            new EventId(713683, "ValidateCommand"),
                            "ValidateCommand failed."
                        );
                        AnsiConsole.MarkupLine($"[red]Validation failed: {result.Error}[/]");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        new EventId(675177, "ValidateCommand"),
                        "ValidateCommand failed."
                    );
                    AnsiConsole.MarkupLine($"[red]Validation failed: {ex.Message}[/]");
                }
            }
            else
            {
                // Normal mode with spinner
                await AnsiConsole
                    .Status()
                    .StartAsync(
                        $"Validating {settings.Standard.ToUpperInvariant()} compliance...",
                        async ctx =>
                        {
                            ctx.Status("Loading image...");
                            var imageData = await File.ReadAllBytesAsync(settings.InputPath);
                            image = RgbImage.Load(imageData);

                            // Process with face alignment for visualization
                            if (settings.Visualize || settings.SharpnessDetails)
                            {
                                ctx.Status("Detecting and aligning face...");
                                var faceResult = await _faceDetector.DetectFacesAsync(image);
                                if (faceResult.IsSuccess && faceResult.Value.Any())
                                {
                                    detectedFace = faceResult
                                        .Value.OrderByDescending(f => f.BoundingBox.Area)
                                        .First();

                                    ctx.Status("Processing with PIV alignment...");
                                    var processingOptions = PivProcessingOptions.Default;
                                    var alignedResult = await PivLandmarkProcessor.ProcessAsync(
                                        image,
                                        detectedFace,
                                        _landmarkExtractor,
                                        processingOptions,
                                        _logger
                                    );

                                    if (alignedResult.IsSuccess)
                                    {
                                        var pivResult = alignedResult.Value;
                                        alignedImage = pivResult.PivImage.Clone(); // Clone for our use
                                        landmarks = pivResult.Landmarks;
                                        _logger.LogDebug(
                                            new EventId(749183, "ValidateCommand"),
                                            "ValidateCommand stage completed."
                                        );
                                    }
                                    else
                                    {
                                        _logger.LogWarning(
                                            new EventId(670734, "ValidateCommand"),
                                            "ValidateCommand requires review."
                                        );
                                        // Fallback to original landmarks
                                        var landmarkResult =
                                            await _landmarkExtractor.ExtractLandmarksAsync(
                                                image,
                                                detectedFace.BoundingBox
                                            );
                                        if (landmarkResult.IsSuccess)
                                        {
                                            landmarks = landmarkResult.Value;
                                        }
                                    }
                                }
                            }

                            ctx.Status(
                                $"Validating {settings.Standard.ToUpperInvariant()} compliance..."
                            );

                            try
                            {
                                var assessmentMode = ParseAssessmentMode(settings.Mode);
                                var result = await imageData.ValidateComplianceAsync(
                                    settings.Standard,
                                    settings.MinConfidence,
                                    _logger,
                                    assessmentMode
                                );
                                if (result.IsSuccess)
                                {
                                    assessment = result.Value;
                                    success = true;
                                }
                                else
                                {
                                    _logger.LogError(
                                        new EventId(789144, "ValidateCommand"),
                                        "ValidateCommand failed."
                                    );
                                    AnsiConsole.MarkupLine(
                                        $"[red]Validation failed: {result.Error}[/]"
                                    );
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(
                                    new EventId(615476, "ValidateCommand"),
                                    "ValidateCommand failed."
                                );
                                AnsiConsole.MarkupLine($"[red]Validation failed: {ex.Message}[/]");
                            }
                        }
                    );
            }

            if (success && assessment != null)
            {
                DisplayComplianceResult(assessment, settings);

                // Show sharpness details if requested
                if (settings.SharpnessDetails && landmarks != null)
                {
                    var imageForAnalysis = alignedImage ?? image;
                    if (imageForAnalysis != null)
                    {
                        await DisplaySharpnessDetails(
                            imageForAnalysis,
                            landmarks,
                            settings,
                            _logger
                        );
                    }
                }

                // Create visualization if requested
                if (settings.Visualize && detectedFace != null && landmarks != null)
                {
                    var imageForVisualization = alignedImage ?? image;
                    if (imageForVisualization != null)
                    {
                        await CreateVisualization(
                            imageForVisualization,
                            detectedFace,
                            landmarks,
                            assessment,
                            settings
                        );
                    }
                }

                var exitCode = assessment.IsCompliant ? 0 : 1;
                image?.Dispose();
                alignedImage?.Dispose();
                return exitCode;
            }

            image?.Dispose();
            alignedImage?.Dispose();
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(879362, "ValidateCommand"), "ValidateCommand failed.");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static Task DisplaySharpnessDetails(
        RgbImage image,
        FaceLandmarks68 landmarks,
        Settings settings,
        ILogger logger
    )
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Sharpness Analysis Details[/]"));

        // Calculate face ellipse
        var ellipseResult = FaceEllipseCalculator.CalculateFromLandmarks(landmarks);
        if (ellipseResult.IsFailure)
        {
            AnsiConsole.MarkupLine(
                $"[red]Failed to calculate face ellipse: {ellipseResult.Error}[/]"
            );
            return Task.CompletedTask;
        }

        var ellipse = ellipseResult.Value;

        // Full image analysis
        var fullImageResult = image.ToGrayscale();
        if (fullImageResult.IsSuccess)
        {
            var fullAnalysis = LaplacianAnalyzer.Analyze(fullImageResult.Value, logger);
            if (fullAnalysis.IsSuccess)
            {
                var fullMetrics = fullAnalysis.Value;
                AnsiConsole.MarkupLine("[bold]Full Image Analysis:[/]");
                AnsiConsole.MarkupLine($"  Variance: {fullMetrics.Variance:F6}");
                AnsiConsole.MarkupLine($"  Normalized Score: {fullMetrics.NormalizedVariance:F3}");
                AnsiConsole.MarkupLine($"  Kurtosis: {fullMetrics.Kurtosis:F2}");
                AnsiConsole.MarkupLine($"  Max Response: {fullMetrics.MaxResponse:F4}");
            }
        }

        // Face ellipse analysis using proper masked approach
        var ellipseSharpnessResult = EllipticalRoiExtractor.AnalyzeEllipticalSharpness(
            image,
            landmarks,
            logger
        );
        if (ellipseSharpnessResult.IsSuccess)
        {
            // Create metrics with the properly masked variance
            var maskedVariance = ellipseSharpnessResult.Value;
            var roiMetrics = new LaplacianMetrics(
                maskedVariance,
                MathF.Sqrt(maskedVariance),
                0f, // Kurtosis not calculated for masked analysis
                0f, // Max response not calculated for masked analysis
                ImmutableArray<float>.Empty
            );

            var faceEllipseResult = FaceEllipseCalculator.CalculateFromLandmarks(landmarks);
            if (faceEllipseResult.IsSuccess)
            {
                var faceEllipse = faceEllipseResult.Value;
                var bounds = faceEllipse.GetBoundingBox();
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Face Ellipse Analysis:[/]");
                AnsiConsole.MarkupLine(
                    $"  Ellipse Center: ({faceEllipse.Center.X:F0},{faceEllipse.Center.Y:F0})"
                );
                AnsiConsole.MarkupLine(
                    $"  Ellipse Size: {faceEllipse.Width:F0}x{faceEllipse.Height:F0}"
                );
                AnsiConsole.MarkupLine(
                    $"  Rotation: {faceEllipse.AngleRadians * 180f / MathF.PI:F1}°"
                );
                AnsiConsole.MarkupLine($"  Variance: {roiMetrics.Variance:F6}");
                AnsiConsole.MarkupLine($"  Normalized Score: {roiMetrics.NormalizedVariance:F3}");
                AnsiConsole.MarkupLine($"  Kurtosis: {roiMetrics.Kurtosis:F2}");
                AnsiConsole.MarkupLine($"  Max Response: {roiMetrics.MaxResponse:F4}");

                // Quality interpretation
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Interpretation:[/]");

                if (roiMetrics.NormalizedVariance >= 0.8f)
                {
                    AnsiConsole.MarkupLine(
                        "[green]• Excellent sharpness - professional quality[/]"
                    );
                }
                else if (roiMetrics.NormalizedVariance >= 0.5f)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]• Good sharpness - acceptable for most uses[/]"
                    );
                }
                else if (roiMetrics.NormalizedVariance >= 0.3f)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]• Moderate sharpness - may need improvement[/]"
                    );
                    AnsiConsole.MarkupLine("  💡 Check focus on eyes and face");
                    AnsiConsole.MarkupLine("  💡 Use faster shutter speed");
                }
                else if (roiMetrics.NormalizedVariance >= 0.1f)
                {
                    AnsiConsole.MarkupLine(
                        "[red]• Poor sharpness - likely motion blur or defocus[/]"
                    );
                    AnsiConsole.MarkupLine("  💡 Use tripod to eliminate camera shake");
                    AnsiConsole.MarkupLine("  💡 Ensure proper autofocus on face");
                    AnsiConsole.MarkupLine("  💡 Increase lighting for faster shutter");
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        "[red]• Very poor sharpness - significant blur detected[/]"
                    );
                    AnsiConsole.MarkupLine("  💡 Image may be out of focus");
                    AnsiConsole.MarkupLine("  💡 Check for camera shake or subject movement");
                }

                // Kurtosis interpretation
                if (roiMetrics.Kurtosis > 50)
                {
                    AnsiConsole.MarkupLine(
                        "[green]• High edge definition detected (good focus)[/]"
                    );
                }
                else if (roiMetrics.Kurtosis < 10)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]• Low edge definition (possible soft focus)[/]"
                    );
                }
            }
        }

        return Task.CompletedTask;
    }

    private static async Task CreateVisualization(
        RgbImage image,
        DetectedFace face,
        FaceLandmarks68 landmarks,
        ComplianceAssessment assessment,
        Settings settings
    )
    {
        var outputPath = settings.VizOutput;
        if (string.IsNullOrEmpty(outputPath))
        {
            var dir = Path.GetDirectoryName(settings.InputPath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(settings.InputPath);
            outputPath = Path.Combine(dir, $"{name}_analysis.png");
        }

        // Clone image for annotation
        using var annotated = image.Clone();

        // Calculate face ellipse
        var ellipseResult = FaceEllipseCalculator.CalculateFromLandmarks(landmarks);
        if (ellipseResult.IsFailure)
        {
            AnsiConsole.MarkupLine($"[red]Failed to calculate face ellipse for visualization[/]");
            return;
        }

        var ellipse = ellipseResult.Value;

        // Apply darkening effect outside the ellipse
        annotated.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < annotated.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    var point = new Point2D(x, y);
                    if (!ellipse.Contains(point))
                    {
                        // Darken pixels outside the ellipse
                        var pixel = row[x];
                        row[x] = new RgbaPixel(
                            (byte)(pixel.R * 0.3f),
                            (byte)(pixel.G * 0.3f),
                            (byte)(pixel.B * 0.3f),
                            pixel.A
                        );
                    }
                }
            }
        });

        annotated.Mutate(ctx =>
        {
            // Draw ellipse outline
            DrawEllipse(ctx, ellipse, OverlayColor.Green, 3);

            // Draw face bounding box calculated from aligned landmarks
            var minX = landmarks.Points.Min(p => p.X);
            var maxX = landmarks.Points.Max(p => p.X);
            var minY = landmarks.Points.Min(p => p.Y);
            var maxY = landmarks.Points.Max(p => p.Y);
            var padding = 20f; // Add some padding around the landmarks
            var faceRect = new RectangleF(
                minX - padding,
                minY - padding,
                (maxX - minX) + 2 * padding,
                (maxY - minY) + 2 * padding
            );
            ctx.Draw(OverlayColor.Blue.WithAlpha(0.5f), 1, faceRect);

            // Draw all 68 facial landmarks
            for (int i = 0; i < landmarks.Points.Count; i++)
            {
                var point = landmarks.Points[i];
                var color = GetLandmarkColor(i);
                var circle = new EllipsePolygon(point.X, point.Y, 2);
                ctx.Fill(color, circle);
            }

            // Draw key facial points with labels
            DrawLabeledPoint(ctx, landmarks.Points[30], "Nose tip", OverlayColor.Yellow);
            DrawLabeledPoint(ctx, landmarks.Points[36], "Left eye", OverlayColor.Cyan);
            DrawLabeledPoint(ctx, landmarks.Points[45], "Right eye", OverlayColor.Cyan);
            DrawLabeledPoint(ctx, landmarks.Points[48], "Left mouth", OverlayColor.Magenta);
            DrawLabeledPoint(ctx, landmarks.Points[54], "Right mouth", OverlayColor.Magenta);

            // Add legend
            var legendY = 10;
            const int lineHeight = 25;

            ctx.DrawText(
                "Legend:",
                OverlayFonts.CreateFont("Arial", 16, OverlayFontStyle.Bold),
                OverlayColor.White,
                new PointF(10, legendY)
            );
            legendY += lineHeight;

            ctx.Draw(OverlayColor.Blue, 3, new RectangleF(10, legendY, 20, 20));
            ctx.DrawText(
                "Face Detection Box",
                OverlayFonts.CreateFont("Arial", 14),
                OverlayColor.Blue,
                new PointF(35, legendY)
            );
            legendY += lineHeight;

            ctx.Draw(OverlayColor.Green, 2, new RectangleF(10, legendY, 20, 20));
            ctx.DrawText(
                "Sharpness Analysis ROI",
                OverlayFonts.CreateFont("Arial", 14),
                OverlayColor.Green,
                new PointF(35, legendY)
            );
            legendY += lineHeight;

            ctx.Fill(OverlayColor.Red, new EllipsePolygon(20, legendY + 10, 5));
            ctx.DrawText(
                "68 Facial Landmarks",
                OverlayFonts.CreateFont("Arial", 14),
                OverlayColor.Red,
                new PointF(35, legendY)
            );
            legendY += lineHeight;

            // Add sharpness score
            legendY += 30;
            var sharpnessColor = assessment.Sharpness.Passed
                ? OverlayColor.Green
                : OverlayColor.Red;
            ctx.DrawText(
                $"Sharpness: {assessment.Sharpness.OverallSharpnessPercent:F1}%",
                OverlayFonts.CreateFont("Arial", 16),
                sharpnessColor,
                new PointF(10, legendY)
            );
        });

        // Keep the encoded format consistent with the filename chosen for the overlay.
        switch (Path.GetExtension(outputPath).ToLowerInvariant())
        {
            case ".png":
                await annotated.SaveAsPngAsync(outputPath);
                break;
            case ".jpg":
            case ".jpeg":
                await annotated.SaveAsJpegAsync(outputPath);
                break;
            default:
                throw new ArgumentException(
                    "Visualization output must use a .png, .jpg, or .jpeg extension.",
                    nameof(settings)
                );
        }
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Visualization saved to: {outputPath}[/]");
    }

    private static void DrawLabeledPoint(
        RgbImage ctx,
        Point2D point,
        string label,
        OverlayColor color
    )
    {
        var circle = new EllipsePolygon(point.X, point.Y, 5);
        ctx.Fill(color, circle);
        ctx.DrawText(
            label,
            OverlayFonts.CreateFont("Arial", 12),
            color,
            new PointF(point.X + 8, point.Y - 6)
        );
    }

    private static void DrawEllipse(
        RgbImage ctx,
        FaceEllipse ellipse,
        OverlayColor color,
        float thickness
    )
    {
        // Generate points along the ellipse perimeter
        const int numPoints = 100;
        var points = new PointF[numPoints];

        for (int i = 0; i < numPoints; i++)
        {
            var angle = 2 * MathF.PI * i / numPoints;
            var x = ellipse.Width / 2 * MathF.Cos(angle);
            var y = ellipse.Height / 2 * MathF.Sin(angle);

            // Rotate by ellipse angle
            var cos = MathF.Cos(ellipse.AngleRadians);
            var sin = MathF.Sin(ellipse.AngleRadians);
            var rx = x * cos - y * sin;
            var ry = x * sin + y * cos;

            // Translate to center
            points[i] = new PointF(ellipse.Center.X + rx, ellipse.Center.Y + ry);
        }

        // Draw the ellipse as a polygon
        ctx.DrawPolygon(color, thickness, points);
    }

    private static OverlayColor GetLandmarkColor(int landmarkIndex)
    {
        return landmarkIndex switch
        {
            >= 0 and <= 16 => OverlayColor.Red, // Jaw line (0-16)
            >= 17 and <= 21 => OverlayColor.Orange, // Left eyebrow (17-21)
            >= 22 and <= 26 => OverlayColor.Orange, // Right eyebrow (22-26)
            >= 27 and <= 35 => OverlayColor.Yellow, // Nose (27-35)
            >= 36 and <= 41 => OverlayColor.Cyan, // Left eye (36-41)
            >= 42 and <= 47 => OverlayColor.Cyan, // Right eye (42-47)
            >= 48 and <= 67 => OverlayColor.Magenta, // Mouth (48-67)
            _ => OverlayColor.White, // Fallback
        };
    }

    private static void DisplayComplianceResult(ComplianceAssessment assessment, Settings settings)
    {
        AnsiConsole.WriteLine();

        // Show mode information
        var modeDescription =
            settings.Mode.ToLowerInvariant() == "input"
                ? "input validation"
                : "credential compliance";
        AnsiConsole.MarkupLine($"[grey]Mode: {modeDescription}[/]");
        AnsiConsole.WriteLine();

        if (assessment.IsCompliant)
        {
            AnsiConsole.MarkupLine($"[green]✅ {assessment.AppliedStandard.Name} COMPLIANT[/]");

            if (settings.ShowDetailed)
            {
                AnsiConsole.WriteLine();
                var detailsTable = new Table()
                    .Border(TableBorder.Rounded)
                    .Title("[bold green]Compliance Details[/]");
                detailsTable.AddColumn("Metric");
                detailsTable.AddColumn("Status");
                detailsTable.AddColumn("Details");

                // Symmetry
                var symStatus = assessment.Symmetry.Passed ? "[green]✅ PASS[/]" : "[red]❌ FAIL[/]";
                var symDetails =
                    $"Illumination: {assessment.Symmetry.IlluminationAsymmetryPercent:F1}%, Pose: {assessment.Symmetry.PoseAsymmetryPercent:F1}%";
                detailsTable.AddRow("Symmetry", symStatus, symDetails);

                // Sharpness
                var sharpStatus = assessment.Sharpness.Passed
                    ? "[green]✅ PASS[/]"
                    : "[red]❌ FAIL[/]";
                var sharpDetails = $"Overall: {assessment.Sharpness.OverallSharpnessPercent:F1}%";
                detailsTable.AddRow("Sharpness", sharpStatus, sharpDetails);

                // Geometry
                var geoStatus = assessment.Geometry.Passed ? "[green]✅ PASS[/]" : "[red]❌ FAIL[/]";
                var geoDetails =
                    $"Head: {assessment.Geometry.HeadSize.SizePercent:F1}%, IPD: {assessment.Geometry.InterPupillaryDistance.DistancePixels:F0}px";
                detailsTable.AddRow("Geometry", geoStatus, geoDetails);

                AnsiConsole.Write(detailsTable);
            }
        }
        else
        {
            AnsiConsole.MarkupLine(
                $"[red]❌ REJECTED for {assessment.AppliedStandard.Name.ToUpperInvariant()} use ({assessment.Rejections.Count} violations)[/]"
            );

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]SPECIFIC REJECTIONS:[/]");

            foreach (var rejection in assessment.Rejections)
            {
                AnsiConsole.MarkupLine($"[red]• {rejection.Message}[/]");

                if (settings.ShowDetailed)
                {
                    var coaching = GetComplianceCoaching(
                        rejection.Metric,
                        rejection.ActualValue,
                        rejection.RequiredValue
                    );
                    foreach (var tip in coaching)
                    {
                        AnsiConsole.MarkupLine($"[blue]  💡 {tip}[/]");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates if the provided mode string is valid
    /// </summary>
    private static bool IsValidMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "input" => true,
            "output" => true,
            _ => false,
        };
    }

    /// <summary>
    /// Parses the mode string to AssessmentMode enum
    /// </summary>
    private static AssessmentMode ParseAssessmentMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "input" => AssessmentMode.InputValidation,
            "output" => AssessmentMode.OutputValidation,
            _ => AssessmentMode.InputValidation, // Default fallback
        };
    }

    private static List<string> GetComplianceCoaching(string metric, float actual, float required)
    {
        return metric switch
        {
            "Illumination Asymmetry" => new List<string>
            {
                "Use even lighting from both sides of the face",
                "Avoid harsh shadows on one side",
                "Position subject directly facing the light source",
            },
            "Pose Asymmetry" => new List<string>
            {
                "Ensure subject faces directly toward camera",
                "Avoid tilted or turned head positions",
                "Check that both eyes are at the same level",
            },
            "Sharpness" => new List<string>
            {
                "Use faster shutter speed to reduce motion blur",
                "Ensure proper focus on the eyes",
                "Use a tripod to eliminate camera shake",
                "Check that image is not over-compressed",
                "Avoid shooting through glass or other obstructions",
            },
            "Head Size" => new List<string>
            {
                "Move closer to subject or zoom in",
                "Ensure head fills appropriate portion of frame",
                "Check framing guidelines for your ID standard",
            },
            _ => new List<string> { "Review general photography guidelines" },
        };
    }
}
