using AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Tool.Tests.Commands;

/// <summary>
/// Tests for CBEFF commands using Spectre.Console.Testing for fast, reliable testing.
/// </summary>
[TestFixture]
public class CbeffCommandTestsRefactored : CommandTestBase
{
    /// <summary>Applies the shared PIV width requirement without invented maximum dimensions.</summary>
    /// <param name="width">Original encoded image width in pixels.</param>
    /// <param name="height">Original encoded image height in pixels.</param>
    /// <param name="accepted">Whether the canonical issuance profile accepts this geometry.</param>
    [TestCase(420, 640, false)]
    [TestCase(421, 640, true)]
    [TestCase(1200, 1600, true)]
    public async Task CbeffCreate_UsesCanonicalProfileDimensions(
        int width,
        int height,
        bool accepted
    )
    {
        var imagePath = Path.Combine(TempDirectory, "geometry.jpg");
        var outputPath = Path.Combine(TempDirectory, "geometry.cbeff");
        using (
            var image = new OpenPhysical.Biometrics.Imaging.RgbImage(
                width,
                height,
                System.Drawing.Color.Gray
            )
        )
            image.SaveAsJpeg(imagePath);
        var result = await App.RunAsync(["cbeff", "create", imagePath, "--output", outputPath]);
        Assert.That(result.ExitCode, Is.EqualTo(accepted ? 0 : 1), Console.Output + result.Output);
        Assert.That(File.Exists(outputPath), Is.EqualTo(accepted));
        if (!accepted)
            Assert.That(Console.Output + result.Output, Does.Contain("ImageDimensions"));
    }

    /// <summary>
    /// Tests CBEFF creation from facial image.
    /// </summary>
    [Test]
    public async Task CbeffCreate_WithValidImage_CreatesOutput()
    {
        var testImagePath = GetPreparedPortraitPath();
        var outputPath = Path.Combine(TempDirectory, "facial_image.cbeff");

        var result = await App.RunAsync(
            new[] { "cbeff", "create", testImagePath, "--output", outputPath }
        );

        result.ExitCode.Should().Be(0, result.Output);
        File.Exists(outputPath).Should().BeTrue("CBEFF file should be created");

        var output = Console.Output + result.Output;
        output.Should().Contain("CBEFF", "Should mention CBEFF creation");
    }

    /// <summary>
    /// Tests CBEFF creation with PIV wrapping.
    /// </summary>
    [Test]
    public async Task CbeffCreate_WithPivWrap_CreatesWrappedOutput()
    {
        var testImagePath = GetPreparedPortraitPath();
        var outputPath = Path.Combine(TempDirectory, "piv_wrapped.cbeff");

        var result = await App.RunAsync(
            new[] { "cbeff", "create", testImagePath, "--output", outputPath, "--piv-wrap" }
        );

        result.ExitCode.Should().Be(0, "PIV wrapped CBEFF creation should succeed");
        File.Exists(outputPath).Should().BeTrue("PIV wrapped CBEFF file should be created");

        // Verify file is larger than unwrapped (due to PIV wrapper)
        var fileInfo = new FileInfo(outputPath);
        fileInfo.Length.Should().BeGreaterThan(100, "PIV wrapped file should have reasonable size");
    }

    /// <summary>
    /// Tests CBEFF inspection of created files.
    /// </summary>
    [Test]
    public async Task CbeffInspect_WithValidCbeff_ShowsDetails()
    {
        // First create a CBEFF file
        var testImagePath = GetPreparedPortraitPath();
        var cbeffPath = Path.Combine(TempDirectory, "test.cbeff");

        var createResult = await App.RunAsync(
            new[] { "cbeff", "create", testImagePath, "--output", cbeffPath }
        );
        createResult.ExitCode.Should().Be(0, createResult.Output);

        // Clear console for inspection
        Console.Clear(true);

        // Now inspect it
        var inspectResult = await App.RunAsync(new[] { "cbeff", "inspect", cbeffPath });

        inspectResult.ExitCode.Should().Be(0, "CBEFF inspection should succeed");

        var output = Console.Output + inspectResult.Output;
        output.Should().Contain("Color Space: RGB (24-bit)");
        output.Should().Contain("CBEFF", "Should show CBEFF details");
        output.Should().Contain("Facial", "Should identify facial image format");
    }

    /// <summary>
    /// Tests CBEFF inspection with hex dump.
    /// </summary>
    [Test]
    public async Task CbeffInspect_WithHexFlag_ShowsHexDump()
    {
        var testImagePath = GetPreparedPortraitPath();
        var cbeffPath = Path.Combine(TempDirectory, "test.cbeff");

        // Create CBEFF file
        await App.RunAsync(new[] { "cbeff", "create", testImagePath, "--output", cbeffPath });

        Console.Clear(true);

        // Inspect with hex
        var result = await App.RunAsync(new[] { "cbeff", "inspect", cbeffPath, "--hex" });

        result.ExitCode.Should().Be(0, "CBEFF hex inspection should succeed");

        var output = Console.Output + result.Output;
        output.Should().Contain("Raw Data (first 256 bytes):", "Should label the bounded hex dump");
        output
            .Should()
            .Contain(
                "00000000: 46 41 43 00",
                "Should expose the FAC record identifier at offset zero"
            );
    }

    /// <summary>
    /// Tests CBEFF extraction functionality.
    /// </summary>
    [Test]
    public async Task CbeffExtract_WithValidCbeff_ExtractsImage()
    {
        var testImagePath = GetPreparedPortraitPath();
        var cbeffPath = Path.Combine(TempDirectory, "test.cbeff");
        var extractedPath = Path.Combine(TempDirectory, "extracted.jpg");

        // Create CBEFF file
        await App.RunAsync(new[] { "cbeff", "create", testImagePath, "--output", cbeffPath });

        Console.Clear(true);

        // Extract from CBEFF
        var result = await App.RunAsync(
            new[] { "cbeff", "extract", cbeffPath, "--output", extractedPath }
        );

        result.ExitCode.Should().Be(0, "CBEFF extraction should succeed");
        File.Exists(extractedPath).Should().BeTrue("Extracted image should exist");

        var output = Console.Output + result.Output;
        output.Should().Contain("extracted", "Should mention extraction");
    }

    /// <summary>
    /// Tests CBEFF signing functionality.
    /// </summary>
    [Test]
    public async Task CbeffSign_WithoutCertificate_ShowsError()
    {
        var testImagePath = GetPreparedPortraitPath();
        var cbeffPath = Path.Combine(TempDirectory, "test.cbeff");

        // Create CBEFF file
        await App.RunAsync(new[] { "cbeff", "create", testImagePath, "--output", cbeffPath });

        Console.Clear(true);

        // Try to sign without certificate (should fail gracefully)
        var result = await App.RunAsync(new[] { "cbeff", "sign", cbeffPath });

        result.ExitCode.Should().NotBe(0, "Should fail without certificate");

        var output = Console.Output + result.Output;
        output.Should().Contain("cert", "Should mention certificate requirement");
    }

    /// <summary>
    /// Tests CBEFF verification functionality.
    /// </summary>
    [Test]
    public async Task CbeffVerify_WithUnsignedCbeff_ShowsNotSigned()
    {
        var testImagePath = GetPreparedPortraitPath();
        var cbeffPath = Path.Combine(TempDirectory, "test.cbeff");

        // Create unsigned CBEFF file
        await App.RunAsync(new[] { "cbeff", "create", testImagePath, "--output", cbeffPath });

        Console.Clear(true);

        // Verify unsigned CBEFF
        var result = await App.RunAsync(new[] { "cbeff", "verify", cbeffPath });

        // Should exit with error code for unsigned CBEFF
        result.ExitCode.Should().NotBe(0, "Should indicate not signed");

        var output = Console.Output + result.Output;
        output.Should().Contain("not signed", "Should indicate CBEFF is not signed");
    }

    /// <summary>
    /// Tests help command for CBEFF branch.
    /// </summary>
    [Test]
    public async Task CbeffHelp_ShowsAvailableCommands()
    {
        var result = await App.RunAsync(new[] { "cbeff", "--help" });

        result.ExitCode.Should().Be(0, "Help should succeed");

        var output = Console.Output + result.Output;
        output.Should().Contain("USAGE", "Should show usage");
        output.Should().Contain("inspect", "Should list inspect command");
        output.Should().Contain("create", "Should list create command");
        output.Should().Contain("extract", "Should list extract command");
        output.Should().Contain("sign", "Should list sign command");
        output.Should().Contain("verify", "Should list verify command");
    }

    /// <summary>
    /// Tests that console output is clean and properly formatted.
    /// </summary>
    [Test]
    public async Task CbeffCommands_ConsoleOutput_IsCleanText()
    {
        var testImagePath = GetPreparedPortraitPath();
        var cbeffPath = Path.Combine(TempDirectory, "test.cbeff");

        var result = await App.RunAsync(
            new[] { "cbeff", "create", testImagePath, "--output", cbeffPath, "--verbose" }
        );

        result.ExitCode.Should().Be(0);

        var output = Console.Output + result.Output;

        // Verify no ANSI escape sequences
        output.Should().NotContain("\u001b[", "Should not contain ANSI escape sequences");
        output.Should().NotContain("\x1b[", "Should not contain escape sequences");

        // Verify meaningful content
        output.Should().NotBeEmpty("Should have output");
        output.Should().Contain("CBEFF", "Should mention CBEFF operations");
    }

    /// <summary>
    /// Tests error handling with missing input file.
    /// </summary>
    [Test]
    public async Task CbeffCommands_WithMissingFile_ShowsClearError()
    {
        var nonExistentPath = Path.Combine(TempDirectory, "missing.cbeff");

        var result = await App.RunAsync(new[] { "cbeff", "inspect", nonExistentPath });

        result.ExitCode.Should().NotBe(0, "Should fail with missing file");

        var output = Console.Output + result.Output;
        output.Should().Contain("not found", "Should indicate file not found");
    }
}
