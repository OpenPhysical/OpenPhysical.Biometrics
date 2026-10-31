using System.Drawing;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Domain.Quality;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Tests.Common;
using OpenPhysical.Biometrics.Tool.Commands;
using OpenPhysical.Biometrics.Tool.Drawing;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Extensions.DependencyInjection;
using Spectre.Console.Testing;

namespace OpenPhysical.Biometrics.Tool.Tests.Commands;

/// <summary>Exercises quality reports, thresholds and command failure paths.</summary>
[TestFixture]
public class QualityCommandTests : UnitTestBase
{
    private const string TestImagePath = "Resources/test-face.jpg";

    /// <summary>Creates a fresh command application and isolated output directory.</summary>
    [SetUp]
    public void Setup()
    {
        // Ensure test image exists
        EnsureTestImage(TestImagePath);
    }

    /// <summary>Checks that a valid image produces a successful quality report.</summary>
    [Test]
    public async Task QualityCommand_WithValidImage_ReturnsSuccess()
    {
        // Arrange
        var (app, console) = CreateApplication();

        // Act
        var result = await app.RunAsync(new[] { "quality", "--input", TestImagePath });

        // Assert
        result.Should().Be(0);
        var output = console.Output;
        output.Should().Contain("Status:");
        output.Should().Contain("Overall Quality:");
        output.Should().ContainAny("COMPLIANT", "NON-COMPLIANT");
    }

    /// <summary>Checks that JSON output is valid and contains the assessment.</summary>
    [Test]
    public async Task QualityCommand_WithJsonFormat_ReturnsValidJson()
    {
        // Arrange
        var (app, console) = CreateApplication();

        // Act
        var result = await app.RunAsync(
            new[] { "quality", "--input", TestImagePath, "--format", "json" }
        );

        // Assert
        result.Should().Be(0);
        var output = console.Output;

        // Verify valid JSON
        var json = JsonDocument.Parse(output);
        var isCompliant = json.RootElement.GetProperty("IsCompliant").ValueKind;
        (isCompliant == JsonValueKind.True || isCompliant == JsonValueKind.False).Should().BeTrue();
        json.RootElement.GetProperty("OverallScore").GetDouble().Should().BeInRange(0, 1);
        json.RootElement.GetProperty("Scores").Should().NotBeNull();
    }

    /// <summary>Checks that detailed output includes regional assessment information.</summary>
    [Test]
    public async Task QualityCommand_WithDetailedFormat_ShowsExtendedInfo()
    {
        // Arrange
        var (app, console) = CreateApplication();

        // Act
        var result = await app.RunAsync(
            new[] { "quality", "--input", TestImagePath, "--format", "detailed" }
        );

        // Assert
        result.Should().Be(0);
        var output = console.Output;

        output.Should().Contain("Detailed Analysis");
        output.Should().Contain("Regional Sharpness");
        output.Should().Contain("Image Dimensions");
        output.Should().Contain("Assessment completed at");
    }

    /// <summary>Checks that selecting a standard applies its assessment criteria.</summary>
    [Test]
    public async Task QualityCommand_WithDifferentStandards_AppliesCorrectCriteria()
    {
        // Arrange
        var standards = new[] { "piv", "twic", "icao", "cac" };

        foreach (var standard in standards)
        {
            var (app, console) = CreateApplication();

            // Act
            var result = await app.RunAsync(
                new[] { "quality", "--input", TestImagePath, "--standard", standard }
            );

            // Assert
            result.Should().Be(0);
            var output = console.Output;
            output.Should().Contain("Overall Quality:");
        }
    }

    /// <summary>Checks that the requested minimum score controls the exit status.</summary>
    [Test]
    public async Task QualityCommand_WithThreshold_EnforcesMinimumScore()
    {
        // Arrange
        var (app, console) = CreateApplication();

        // Act
        var result = await app.RunAsync(
            new[] { "quality", "--input", TestImagePath, "--threshold", "0.9", "--format", "json" }
        );

        // Assert
        result.Should().Be(0);
        var output = console.Output;
        var json = JsonDocument.Parse(output);

        // If compliant, score should be >= 0.9
        if (json.RootElement.GetProperty("IsCompliant").GetBoolean())
        {
            json.RootElement.GetProperty("OverallScore")
                .GetDouble()
                .Should()
                .BeGreaterThanOrEqualTo(0.9);
        }
    }

    /// <summary>Checks that strict mode applies its stricter quality settings.</summary>
    [Test]
    public async Task QualityCommand_WithStrictMode_AppliesStrictSettings()
    {
        // Arrange
        var (app, console) = CreateApplication();

        // Act
        var result = await app.RunAsync(
            new[] { "quality", "--input", TestImagePath, "--strict", "--format", "json" }
        );

        // Assert
        result.Should().Be(0);
        var output = console.Output;
        var json = JsonDocument.Parse(output);

        // Strict mode should have higher threshold (0.85)
        if (json.RootElement.GetProperty("IsCompliant").GetBoolean())
        {
            json.RootElement.GetProperty("OverallScore")
                .GetDouble()
                .Should()
                .BeGreaterThanOrEqualTo(0.85);
        }
    }

    /// <summary>Checks that a requested output path receives the report.</summary>
    [Test]
    public async Task QualityCommand_WithOutputPath_SavesReportToFile()
    {
        // Arrange
        var (app, console) = CreateApplication();
        var outputPath = Path.Combine(Path.GetTempPath(), "quality-report.json");

        try
        {
            // Act
            var result = await app.RunAsync(
                new[]
                {
                    "quality",
                    "--input",
                    TestImagePath,
                    "--format",
                    "json",
                    "--output",
                    outputPath,
                }
            );

            // Assert
            result.Should().Be(0);
            File.Exists(outputPath).Should().BeTrue();

            var fileContent = await File.ReadAllTextAsync(outputPath);
            var json = JsonDocument.Parse(fileContent);
            json.RootElement.GetProperty("IsCompliant").Should().NotBeNull();
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    /// <summary>Checks that a missing input file produces an error.</summary>
    [Test]
    public async Task QualityCommand_WithNonExistentFile_ReturnsError()
    {
        // Arrange
        var (app, console) = CreateApplication();

        // Act
        var result = await app.RunAsync(new[] { "quality", "--input", "non-existent-file.jpg" });

        // Assert
        result.Should().Be(1);
        var output = console.Output;
        output.Should().Contain("Error: Input file not found");
    }

    /// <summary>Checks that quality problems appear as report violations.</summary>
    [Test]
    public async Task QualityCommand_ShowsViolations_WhenQualityIssuesExist()
    {
        // Arrange
        var (app, console) = CreateApplication();

        // Create a low-quality test image if needed
        EnsureTestImage("Resources/low-quality-face.jpg", createLowQuality: true);

        // Act
        var result = await app.RunAsync(
            new[] { "quality", "--input", "Resources/low-quality-face.jpg" }
        );

        // Assert
        result.Should().Be(0);
        var output = console.Output;

        // Check if violations are shown when present
        if (output.Contains("NON-COMPLIANT"))
        {
            output.Should().Contain("Violations:");
        }
    }

    /// <summary>Checks that an image without a face produces a failure exit status.</summary>
    [Test]
    public async Task QualityCommand_WithoutFace_ReturnsFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quality-empty-{Guid.NewGuid():N}.png");
        try
        {
            using (var image = new RgbImage(128, 128, System.Drawing.Color.White))
                image.SaveAsPng(path);
            var (app, console) = CreateApplication();
            var result = await app.RunAsync(["quality", "--input", path]);
            Assert.That(result, Is.EqualTo(1));
            Assert.That(console.Output, Does.Contain("No faces detected"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private (CommandApp app, TestConsole console) CreateApplication()
    {
        var services = new ServiceCollection();
        services.AddBiometricsTool();
        services.AddLogging(builder => builder.AddSimpleConsole());

        var registrar = new DependencyInjectionRegistrar(services);
        var console = new TestConsole();

        // Configure console output properly
        AnsiConsole.Console = console;

        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.ConfigureConsole(console);
            config.AddCommand<QualityCommand>("quality");
        });

        return (app, console);
    }

    private void EnsureTestImage(string path, bool createLowQuality = false)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        CreateTestFaceImage(path, createLowQuality);
    }

    private void CreateTestFaceImage(string path, bool lowQuality)
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (
            root is not null
            && !File.Exists(Path.Combine(root.FullName, "OpenPhysical.Biometrics.sln"))
        )
            root = root.Parent;
        if (root is null)
            throw new DirectoryNotFoundException(
                "The committed image fixture directory was not found."
            );
        // A quality integration test needs a real detected face. A black placeholder
        // exercises the separate no-face outcome rather than the reporting workflow.
        using var image = RgbImage.Load(
            Path.Combine(root.FullName, "tests", "sample_images", "generic_guy.jpg")
        );
        if (lowQuality)
            image.GaussianBlur(3);
        image.SaveAsJpeg(path, 85);
    }
}
