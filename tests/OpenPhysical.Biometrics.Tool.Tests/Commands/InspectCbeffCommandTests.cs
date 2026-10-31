namespace OpenPhysical.Biometrics.Tool.Tests.Commands;

using System.IO;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Standards.Incits385;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Extensions;
using OpenPhysical.Biometrics.Infrastructure.Standards.Incits385;
using OpenPhysical.Biometrics.Tool.Commands;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Extensions.DependencyInjection;
using Spectre.Console.Testing;

/// <summary>
/// Comprehensive tests for InspectCbeffCommand with CMS verification functionality.
/// </summary>
[TestFixture]
public class InspectCbeffCommandTests
{
    private string _testDataDirectory = null!;

    /// <summary>Loads the signed biometric fixture used by the inspection tests.</summary>
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Find test data directory
        var currentDir = TestContext.CurrentContext.TestDirectory;
        var searchDir = new DirectoryInfo(currentDir);

        while (
            searchDir != null
            && !File.Exists(Path.Combine(searchDir.FullName, "OpenPhysical.Biometrics.sln"))
        )
        {
            searchDir = searchDir.Parent;
        }

        if (searchDir == null)
        {
            throw new InvalidOperationException("Could not find solution root");
        }

        _testDataDirectory = Path.Combine(
            searchDir.FullName,
            "tests",
            "OpenPhysical.Biometrics.Infrastructure.Tests",
            "TestData",
            "GoldenPiv"
        );
    }

    private ServiceCollection CreateTestServices(IAnsiConsole console)
    {
        var services = new ServiceCollection();

        // Configure logging to suppress output during tests
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Critical);
        });

        // Register CBEFF services
        services.AddCbeffServices();
        services.AddCompatibilityCryptographyServices();
        services.AddCertificateStore();
        services.AddScoped<ICbeffEncoder, CbeffEncoderService>();
        services.AddScoped<ICbeffDecoder, CbeffDecoderService>();
        services.AddScoped<ICbeffSigner, CbeffSignerService>();
        services.AddScoped<ICbeffVerifier, CbeffVerifierService>();
        services.AddScoped<ICbeffBuilder, CbeffBuilderService>();

        // Register INCITS 385 services
        services.AddSingleton<IIncits385Writer, Incits385WriterService>();

        // Register metadata services
        services.AddMetadataServices();

        // Register the test console
        services.AddSingleton<IAnsiConsole>(console);

        // Register CLI commands
        services.AddScoped<InspectCbeffCommand>();

        return services;
    }

    private (CommandAppTester app, TestConsole console) CreateTestApp()
    {
        // Create a TestConsole with no ANSI sequences for plain text output
        var console = new TestConsole();
        console.EmitAnsiSequences = false;
        AnsiConsole.Console = console;

        // Create services with the test console
        var services = CreateTestServices(console);

        var app = new CommandAppTester(new DependencyInjectionRegistrar(services));
        app.Configure(config =>
        {
            config.ConfigureConsole(console);
            config.AddCommand<InspectCbeffCommand>("inspect");
        });

        return (app, console);
    }

    /// <summary>Checks that the signed facial fixture exposes its CMS structure.</summary>
    [Test]
    public void InspectCommand_WithGoldenPivFacialImage_ShowsComprehensiveCmsAnalysis()
    {
        // Arrange
        var facialImagePath = Path.Combine(_testDataDirectory, "01_Golden_PIV", "facial_image.bin");
        Assume.That(File.Exists(facialImagePath), "Golden PIV facial image test file must exist");

        var (app, console) = CreateTestApp();

        // Act
        var result = app.Run("inspect", facialImagePath);

        // Assert
        result.ExitCode.Should().Be(0, "Command should execute successfully");

        // Get plain text output from the test console
        var output = console.Output + result.Output;

        // Verify basic CBEFF structure is displayed
        output.Should().Contain("CBEFF Structure", "Should display CBEFF structure tree");
        output.Should().Contain("CBEFF Header", "Should display header information");
        output.Should().Contain("Biometric Data Block", "Should display BDB information");

        // Verify CMS signature analysis
        output.Should().Contain("Signature Block", "Should display signature information");
        output.Should().Contain("Type: CMS", "Should identify CMS signature type");

        // Verify detailed CMS cryptographic analysis
        output.Should().Contain("Digest Algorithms", "Should display digest algorithms");
        output.Should().Contain("SHA-256", "Should identify SHA-256 digest algorithm");
        output.Should().Contain("2.16.840.1.101.3.4.2.1", "Should display SHA-256 OID");

        // Verify signer information
        output.Should().Contain("Signers", "Should display signer information");
        output.Should().Contain("Issuer:", "Should display signer issuer DN");
        output.Should().Contain("Serial Number:", "Should display signer serial number");
        output.Should().Contain("ICAM Test Card Signing CA", "Should display expected test CA");

        // Verify algorithm information
        output.Should().Contain("Digest Algorithm:", "Should display digest algorithm for signer");
        output.Should().Contain("Signature Algorithm:", "Should display the signature algorithm");
        output.Should().Contain("RSA", "Should identify the RSA signature algorithm");

        // Verify signed attributes
        output.Should().Contain("Signed Attributes", "Should display signed attributes");
        output.Should().Contain("Content Type", "Should display content type attribute");
        output.Should().Contain("Message Digest", "Should display message digest attribute");
    }

    /// <summary>Checks that the hex option displays the signature bytes.</summary>
    [Test]
    public void InspectCommand_WithHexFlag_ShowsHexDumpOfCmsData()
    {
        // Arrange
        var facialImagePath = Path.Combine(_testDataDirectory, "01_Golden_PIV", "facial_image.bin");
        Assume.That(File.Exists(facialImagePath), "Golden PIV facial image test file must exist");

        var (app, console) = CreateTestApp();

        // Act
        var result = app.Run("inspect", facialImagePath, "--hex");

        // Assert
        result.ExitCode.Should().Be(0, "Command should execute successfully");

        var output = console.Output + result.Output;

        // Verify hex dumps are included
        output
            .Should()
            .Contain("Raw Data (first 256 bytes):", "Should display biometric data hex dump");
        output
            .Should()
            .Contain("Encoded Data (first 256 bytes):", "Should display CMS encoded data hex dump");

        // Verify hex format (addresses and hex bytes)
        output.Should().MatchRegex(@"[0-9A-F]{8}:", "Should contain hex addresses");
        output
            .Should()
            .MatchRegex(@"[0-9A-F]{2}\s+[0-9A-F]{2}", "Should contain hex byte sequences");
    }

    /// <summary>Checks that a parsed facial record exposes its INCITS 385 fields.</summary>
    [Test]
    public void InspectCommand_WithParsedBiometricRecord_ShowsIncits385Details()
    {
        // Arrange
        var facialImagePath = Path.Combine(_testDataDirectory, "01_Golden_PIV", "facial_image.bin");
        Assume.That(File.Exists(facialImagePath), "Golden PIV facial image test file must exist");

        var (app, console) = CreateTestApp();

        // Act
        var result = app.Run("inspect", facialImagePath);

        // Assert
        result.ExitCode.Should().Be(0, "Command should execute successfully");

        var output = console.Output + result.Output;

        // Verify parsed biometric record information
        output
            .Should()
            .Contain("INCITS 385 Facial Image Record", "Should display parsed biometric record");
        output.Should().Contain("Facial Information", "Should identify as facial record type");

        // Verify INCITS 385 specific information is displayed
        output
            .Should()
            .Contain(
                "BDB Format Owner: 001B (INCITS M1 (Biometrics))",
                "Should identify INCITS as format owner"
            );
        output
            .Should()
            .Contain(
                "BDB Format Type: 0501 (INCITS 385 Facial Image)",
                "Should identify facial image format type"
            );
    }

    /// <summary>Checks that a missing input file produces a readable error.</summary>
    [Test]
    public void InspectCommand_WithInvalidFile_ReturnsErrorAndShowsMessage()
    {
        // Arrange
        var invalidFilePath = "nonexistent_file.bin";

        var (app, console) = CreateTestApp();

        // Act
        var result = app.Run("inspect", invalidFilePath);

        // Assert
        result.ExitCode.Should().Be(1, "Command should return error code for invalid file");

        var output = console.Output + result.Output;
        output.Should().Contain("File not found", "Should display file not found error");
        output.Should().Contain(invalidFilePath, "Should mention the specific file path");
    }

    /// <summary>Checks that malformed CBEFF data produces a parsing error.</summary>
    [Test]
    public void InspectCommand_WithCorruptedCbeffData_ShowsParseError()
    {
        // Arrange - Create a temporary file with invalid CBEFF data
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[] { 0x01, 0x02, 0x03, 0x04 }); // Invalid CBEFF data

            var (app, console) = CreateTestApp();

            // Act
            var result = app.Run("inspect", tempFile);

            // Assert
            result.ExitCode.Should().Be(1, "Command should return error code for corrupted data");

            var output = console.Output + result.Output;
            output.Should().Contain("Failed to parse CBEFF", "Should display parse error message");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>Checks that extraction writes the embedded biometric data.</summary>
    [Test]
    public void InspectCommand_WithExtractOption_ExtractsBiometricData()
    {
        // Arrange
        var facialImagePath = Path.Combine(_testDataDirectory, "01_Golden_PIV", "facial_image.bin");
        var extractPath = Path.GetTempFileName();

        try
        {
            Assume.That(
                File.Exists(facialImagePath),
                "Golden PIV facial image test file must exist"
            );

            var (app, console) = CreateTestApp();

            // Act
            var result = app.Run("inspect", facialImagePath, "--extract", extractPath);

            // Assert
            result.ExitCode.Should().Be(0, "Command should execute successfully");

            var output = console.Output + result.Output;
            output
                .Should()
                .Contain("Extracted biometric data to", "Should confirm data extraction");
            output.Should().Contain(extractPath, "Should mention extract path");

            // Verify extracted file exists and has content
            File.Exists(extractPath).Should().BeTrue("Extracted file should exist");
            var extractedData = File.ReadAllBytes(extractPath);
            extractedData.Length.Should().BeGreaterThan(0, "Extracted data should not be empty");
        }
        finally
        {
            if (File.Exists(extractPath))
            {
                File.Delete(extractPath);
            }
        }
    }

    /// <summary>Checks that known algorithm OIDs have readable names.</summary>
    [Test]
    public void InspectCommand_VerifiesOidMappingsForKnownAlgorithms()
    {
        // Arrange
        var facialImagePath = Path.Combine(_testDataDirectory, "01_Golden_PIV", "facial_image.bin");
        Assume.That(File.Exists(facialImagePath), "Golden PIV facial image test file must exist");

        var (app, console) = CreateTestApp();

        // Act
        var result = app.Run("inspect", facialImagePath);

        // Assert
        result.ExitCode.Should().Be(0, "Command should execute successfully");

        var output = console.Output + result.Output;

        // Verify specific OID mappings are correctly displayed
        var expectedMappings = new Dictionary<string, string>
        {
            { "2.16.840.1.101.3.4.2.1", "SHA-256" },
            { "1.2.840.113549.1.1.1", "RSA" },
            { "1.2.840.113549.1.9.3", "Content Type" },
            { "1.2.840.113549.1.9.4", "Message Digest" },
        };

        foreach (var mapping in expectedMappings)
        {
            output
                .Should()
                .Contain(
                    $"{mapping.Value} ({mapping.Key})",
                    $"Should correctly map OID {mapping.Key} to {mapping.Value}"
                );
        }
    }

    /// <summary>Checks that signature inspection displays the expected structure.</summary>
    [Test]
    public void InspectCommand_WithSignatureVerification_ShowsProperStructure()
    {
        // Arrange
        var facialImagePath = Path.Combine(_testDataDirectory, "01_Golden_PIV", "facial_image.bin");
        Assume.That(File.Exists(facialImagePath), "Golden PIV facial image test file must exist");

        var (app, console) = CreateTestApp();

        // Act
        var result = app.Run("inspect", facialImagePath);

        // Assert
        result.ExitCode.Should().Be(0, "Command should execute successfully");

        var output = console.Output + result.Output;

        // Verify signature verification section appears
        output
            .Should()
            .Contain("Signature Verification", "Should display signature verification section");

        // The application explicitly supplies the committed ICAM test trust store.
        output.Should().Contain("Signature verification PASSED");
        output.Should().Contain("ICAM PIV Content Signer");
    }

    /// <summary>Checks that the complete report includes each expected section.</summary>
    [Test]
    public void InspectCommand_ComprehensiveOutput_MatchesExpectedStructure()
    {
        // Arrange
        var facialImagePath = Path.Combine(_testDataDirectory, "01_Golden_PIV", "facial_image.bin");
        Assume.That(File.Exists(facialImagePath), "Golden PIV facial image test file must exist");

        var (app, console) = CreateTestApp();

        // Act
        var result = app.Run("inspect", facialImagePath);

        // Assert
        result.ExitCode.Should().Be(0, "Command should execute successfully");

        var output = console.Output + result.Output;

        // Verify the complete expected output structure appears in order
        var expectedSections = new[]
        {
            "Inspecting CBEFF file:",
            "CBEFF Structure",
            "CBEFF Header",
            "Patron Header Version:",
            "BDB Format Owner:",
            "BDB Format Type:",
            "Creation Date:",
            "Biometric Data Block",
            "INCITS 385 Facial Image Record",
            "Signature Block",
            "Type: CMS",
            "Digest Algorithms",
            "SHA-256",
            "Signers",
            "Issuer:",
            "Serial Number:",
            "Digest Algorithm:",
            "Signature Algorithm:",
            "Signed Attributes",
            "Signature Verification",
        };

        foreach (var section in expectedSections)
        {
            output.Should().Contain(section, $"Should contain section: {section}");
        }

        // Verify sections appear in logical order by checking key sections
        var headerIndex = output.IndexOf("CBEFF Header");
        var bdbIndex = output.IndexOf("Biometric Data Block");
        var signatureIndex = output.IndexOf("Signature Block");
        var verificationIndex = output.IndexOf("Signature Verification");

        headerIndex.Should().BeLessThan(bdbIndex, "Header should appear before BDB");
        bdbIndex.Should().BeLessThan(signatureIndex, "BDB should appear before signature");
        signatureIndex
            .Should()
            .BeLessThan(verificationIndex, "Signature should appear before verification");
    }
}
