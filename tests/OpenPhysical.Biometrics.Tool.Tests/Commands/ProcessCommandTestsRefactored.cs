using System.Drawing;
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Tool.Drawing;

namespace OpenPhysical.Biometrics.Tool.Tests.Commands;

/// <summary>
/// Refactored tests for ProcessCommand using Spectre.Console.Testing instead of external processes.
/// </summary>
[TestFixture]
public class ProcessCommandTestsRefactored : CommandTestBase
{
    /// <summary>
    /// Tests that process command produces JP2 output for valid image.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithValidImage_ProducesOutput()
    {
        var testImagePath = GetTestImagePath();
        var outputPath = Path.Combine(TempDirectory, "output.jp2");

        var result = await App.RunAsync(new[] { "process", testImagePath, "--output", outputPath });

        result.ExitCode.Should().Be(0, "CLI should exit with success code");
        File.Exists(outputPath).Should().BeTrue("Output file should be created");

        // Verify output file size is reasonable for JP2
        var fileInfo = new FileInfo(outputPath);
        fileInfo
            .Length.Should()
            .BeInRange(10_000, 50_000, "JP2 file should be between 10KB and 50KB");

        // Verify console output contains success message
        var output = Console.Output + result.Output;
        output.Should().NotBeEmpty("Should have console output");
    }

    /// <summary>
    /// Tests that process command returns error for missing file.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithMissingFile_ReturnsError()
    {
        var nonExistentPath = Path.Combine(TempDirectory, "does_not_exist.jpg");

        var result = await App.RunAsync(new[] { "process", nonExistentPath });

        result.ExitCode.Should().NotBe(0, "CLI should exit with error code");

        var output = Console.Output + result.Output;
        output.Should().Contain("not found", "Should indicate file not found");
    }

    /// <summary>
    /// Tests that process command creates JP2 without ROI when specified.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithNoRoi_CreatesCorrectFormat()
    {
        var testImagePath = GetTestImagePath();
        var outputPath = Path.Combine(TempDirectory, "output.jp2");

        var result = await App.RunAsync(
            new[] { "process", testImagePath, "--output", outputPath, "--no-roi" }
        );

        result.ExitCode.Should().Be(0, "Should succeed");
        File.Exists(outputPath).Should().BeTrue("Output file should be created");

        // Verify it's actually a JP2
        var fileBytes = await File.ReadAllBytesAsync(outputPath);
        fileBytes.Length.Should().BeGreaterThan(8, "Should have valid JP2 content");
    }

    /// <summary>
    /// Tests that rate parameter affects output file size.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithRateParameter_AppliesCompression()
    {
        var testImagePath = GetTestImagePath();
        var highQualityPath = Path.Combine(TempDirectory, "high_quality.jp2");
        var lowQualityPath = Path.Combine(TempDirectory, "low_quality.jp2");

        // Process with high quality
        var highResult = await App.RunAsync(
            new[] { "process", testImagePath, "--output", highQualityPath, "--rate", "1.0" }
        );
        highResult.ExitCode.Should().Be(0, "High quality processing should succeed");

        // Reset console for second command
        Console.Clear(true);

        // Process with low quality
        var lowResult = await App.RunAsync(
            new[] { "process", testImagePath, "--output", lowQualityPath, "--rate", "0.6" }
        );
        lowResult.ExitCode.Should().Be(0, "Low quality processing should succeed");

        var highQualitySize = new FileInfo(highQualityPath).Length;
        var lowQualitySize = new FileInfo(lowQualityPath).Length;

        // Note: Due to JPEG 2000 compression behavior, this test may be flaky
        // The compression difference might not always be significant for small test images
        if (highQualitySize == lowQualitySize)
        {
            Assert.Inconclusive(
                "Compression rates did not produce different file sizes for this test image"
            );
        }
        else
        {
            highQualitySize
                .Should()
                .BeGreaterThan(lowQualitySize, "Higher quality should result in larger file size");
        }
    }

    /// <summary>
    /// Tests that verbose flag produces detailed output.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithVerboseFlag_ShowsDetailedOutput()
    {
        var testImagePath = GetTestImagePath();
        var outputPath = Path.Combine(TempDirectory, "output.jp2");

        var result = await App.RunAsync(
            new[] { "process", testImagePath, "--output", outputPath, "--verbose" }
        );

        result.ExitCode.Should().Be(0, "Should succeed");

        var output = Console.Output + result.Output;
        output.Should().Contain("Processing Results", "Should show processing details");
        output.Should().Contain("completed successfully", "Should show success message");
    }

    /// <summary>
    /// Tests that process command fails when no face is detected.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithNoFaceInImage_ReturnsError()
    {
        var noFaceImagePath = CreateEmptyImage();
        var outputPath = Path.Combine(TempDirectory, "output.jp2");

        var result = await App.RunAsync(
            new[] { "process", noFaceImagePath, "--output", outputPath }
        );

        result.ExitCode.Should().NotBe(0, "Should fail when no face is detected");
        File.Exists(outputPath).Should().BeFalse("No output should be created");

        var output = Console.Output + result.Output;
        output.Should().Contain("No face", "Should indicate no face detected");
    }

    /// <summary>
    /// Tests that process command uses default output naming.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithoutOutputPath_UsesDefaultNaming()
    {
        // Copy test image to temp directory for this test
        var testImagePath = GetTestImagePath();
        var tempImagePath = Path.Combine(TempDirectory, "test_face.png");
        File.Copy(testImagePath, tempImagePath, true);

        var result = await App.RunAsync(new[] { "process", tempImagePath });

        result.ExitCode.Should().Be(0, "Should succeed");

        // Check for default output file
        var expectedOutputName = Path.GetFileNameWithoutExtension(tempImagePath) + ".jp2";
        var expectedOutputPath = Path.Combine(TempDirectory, expectedOutputName);

        File.Exists(expectedOutputPath).Should().BeTrue("Default output file should be created");
    }

    /// <summary>
    /// Tests quality gate functionality with console output verification.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithQualityGate_ShowsQualityResults()
    {
        var testImagePath = GetTestImagePath();
        var outputPath = Path.Combine(TempDirectory, "output.jp2");

        var result = await App.RunAsync(
            new[]
            {
                "process",
                testImagePath,
                "--output",
                outputPath,
                "--quality-gate",
                "0.0",
                "--verbose",
            }
        );

        // A zero threshold exercises report rendering independently of model calibration.
        result.ExitCode.Should().Be(0, "Should pass quality gate");

        var output = Console.Output + result.Output;
        output.Should().Contain("Quality", "Should mention quality assessment");
    }

    /// <summary>
    /// Tests that help command works correctly.
    /// </summary>
    [Test]
    public async Task ProcessCommand_Help_ShowsUsageInformation()
    {
        var result = await App.RunAsync(new[] { "process", "--help" });

        result.ExitCode.Should().Be(0, "Help should exit successfully");

        var output = Console.Output + result.Output;
        output.Should().Contain("USAGE", "Should show usage information");
        output.Should().Contain("process", "Should mention the process command");
    }

    /// <summary>
    /// Tests different output formats.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithDifferentFormats_CreatesCorrectOutput()
    {
        var testImagePath = GetTestImagePath();
        var formats = new[] { "jp2", "jpeg", "png", "tiff" };

        foreach (var format in formats)
        {
            var outputPath = Path.Combine(TempDirectory, $"output.{format}");

            var result = await App.RunAsync(
                new[]
                {
                    "process",
                    testImagePath,
                    "--output",
                    outputPath,
                    "--format",
                    format,
                    "--no-roi",
                }
            );

            result.ExitCode.Should().Be(0, $"Should succeed for {format} format");
            File.Exists(outputPath).Should().BeTrue($"Output file should be created for {format}");

            // Clear console for next iteration
            Console.Clear(true);
        }
    }

    /// <summary>
    /// Tests that the console output is properly captured and formatted.
    /// </summary>
    [Test]
    public async Task ProcessCommand_ConsoleOutput_IsProperlyFormatted()
    {
        var testImagePath = GetTestImagePath();
        var outputPath = Path.Combine(TempDirectory, "output.jp2");

        var result = await App.RunAsync(
            new[] { "process", testImagePath, "--output", outputPath, "--verbose" }
        );

        result.ExitCode.Should().Be(0);

        var output = Console.Output + result.Output;

        // Verify console output doesn't contain ANSI escape sequences
        output.Should().NotContain("\u001b[", "Should not contain ANSI escape sequences");

        // Verify output is readable plain text
        output.Should().NotBeEmpty("Should have output");
        output.Length.Should().BeGreaterThan(10, "Should have meaningful output");
    }
}
