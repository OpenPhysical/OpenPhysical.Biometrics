namespace OpenPhysical.Biometrics.Tool.Tests.Commands;

using System.IO;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenPhysical.Biometrics.Infrastructure.Extensions;
using OpenPhysical.Biometrics.Tool.Commands;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Extensions.DependencyInjection;
using Spectre.Console.Testing;

/// <summary>
/// Fast, reliable tests for ProcessCommand using Spectre.Console.Testing.
/// </summary>
[TestFixture]
public class ProcessCommandTestConsoleTests
{
    private string _testImagePath = null!;
    private string _tempDirectory = null!;

    /// <summary>Creates a fresh command application and isolated output directory.</summary>
    [SetUp]
    public void SetUp()
    {
        // Create temp directory
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);

        // Find test image
        var currentDir = TestContext.CurrentContext.TestDirectory;
        var searchDir = new DirectoryInfo(currentDir);

        while (
            searchDir != null
            && !File.Exists(Path.Combine(searchDir.FullName, "OpenPhysical.Biometrics.sln"))
        )
        {
            searchDir = searchDir.Parent;
        }

        _testImagePath = Path.Combine(
            searchDir!.FullName,
            "tests",
            "sample_images",
            "generic_guy.png"
        );
    }

    /// <summary>Removes generated files and releases the captured console.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private ServiceCollection CreateTestServices(IAnsiConsole console)
    {
        var services = new ServiceCollection();

        // Create hosting environment
        var hostEnvironment = new TestHostEnvironment();
        services.AddSingleton<IHostEnvironment>(hostEnvironment);

        // Configure logging to suppress output during tests
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Critical);
        });

        // Register biometric services
        services.AddBiometricServices();

        // Register certificate store for test environment
        services.AddCertificateStoreForEnvironment(hostEnvironment);

        // Register the test console
        services.AddSingleton<IAnsiConsole>(console);

        // Register commands
        services.AddScoped<ProcessCommand>();

        return services;
    }

    private (CommandAppTester app, TestConsole console) CreateTestApp()
    {
        // Create a TestConsole with no ANSI sequences for plain text output
        var console = new TestConsole();
        AnsiConsole.Console = console;
        console.EmitAnsiSequences = false;

        // Create services with the test console
        var services = CreateTestServices(console);

        var app = new CommandAppTester(new DependencyInjectionRegistrar(services));
        app.Configure(config =>
        {
            config.ConfigureConsole(console);
            config.AddCommand<ProcessCommand>("process");
        });

        return (app, console);
    }

    /// <summary>
    /// Tests that process command produces output for valid image using TestConsole.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithValidImage_ProducesOutputWithTestConsole()
    {
        var (app, console) = CreateTestApp();
        var outputPath = Path.Combine(_tempDirectory, "output.jp2");

        var result = await app.RunAsync(
            new[] { "process", _testImagePath, "--output", outputPath }
        );

        result.ExitCode.Should().Be(0, "CLI should exit with success code");
        File.Exists(outputPath).Should().BeTrue("Output file should be created");

        // Verify console output is captured
        var output = console.Output;
        output.Should().NotBeEmpty("Should have console output");
        output.Should().Contain("✓", "Should contain success indicator");
    }

    /// <summary>
    /// Tests that process command returns error for missing file using TestConsole.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithMissingFile_ReturnsErrorWithTestConsole()
    {
        var (app, console) = CreateTestApp();
        var nonExistentPath = Path.Combine(_tempDirectory, "does_not_exist.jpg");

        var result = await app.RunAsync(new[] { "process", nonExistentPath });

        result.ExitCode.Should().NotBe(0, "CLI should exit with error code");

        var output = console.Output;
        output.Should().Contain("not found", "Should indicate file not found");
    }

    /// <summary>
    /// Tests that verbose flag produces detailed output using TestConsole.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithVerboseFlag_ShowsDetailedOutputWithTestConsole()
    {
        var (app, console) = CreateTestApp();
        var outputPath = Path.Combine(_tempDirectory, "output.jp2");

        var result = await app.RunAsync(
            new[] { "process", _testImagePath, "--output", outputPath, "--verbose" }
        );

        result.ExitCode.Should().Be(0, "Should succeed");

        var output = console.Output;
        output.Should().Contain("Processing Results", "Should show processing details");
        output.Should().Contain("completed successfully", "Should show success message");
    }

    /// <summary>
    /// Tests that console output doesn't contain ANSI sequences with TestConsole.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithTestConsole_OutputIsPlainText()
    {
        var (app, console) = CreateTestApp();
        var outputPath = Path.Combine(_tempDirectory, "output.jp2");

        var result = await app.RunAsync(
            new[] { "process", _testImagePath, "--output", outputPath, "--verbose" }
        );

        result.ExitCode.Should().Be(0);

        var output = console.Output;

        // Verify no ANSI escape sequences since EmitAnsiSequences = false
        output.Should().NotContain("\u001b[", "Should not contain ANSI escape sequences");
        output.Should().NotContain("\x1b[", "Should not contain escape sequences");

        // Verify meaningful content
        output.Should().NotBeEmpty("Should have output");
        output.Should().Contain("Processing Results", "Should contain processing results");
    }

    /// <summary>
    /// Tests process command with no-roi flag using TestConsole.
    /// </summary>
    [Test]
    public async Task ProcessCommand_WithNoRoi_WorksWithTestConsole()
    {
        var (app, console) = CreateTestApp();
        var outputPath = Path.Combine(_tempDirectory, "output.jp2");

        var result = await app.RunAsync(
            new[] { "process", _testImagePath, "--output", outputPath, "--no-roi" }
        );

        result.ExitCode.Should().Be(0, "Should succeed");
        File.Exists(outputPath).Should().BeTrue("Output file should be created");

        var output = console.Output;
        output.Should().Contain("✓", "Should show success");
    }

    /// <summary>
    /// Tests help command using TestConsole.
    /// </summary>
    [Test]
    public async Task ProcessCommand_Help_ShowsUsageWithTestConsole()
    {
        var (app, console) = CreateTestApp();

        var result = await app.RunAsync(new[] { "process", "--help" });

        result.ExitCode.Should().Be(0, "Help should exit successfully");

        var output = result.Output + console.Output;
        output.Should().Contain("USAGE", "Should show usage information");
        output.Should().Contain("process", "Should mention the process command");
    }
}

/// <summary>
/// Test hosting environment for CLI command testing.
/// </summary>
internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "biotool-test";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
