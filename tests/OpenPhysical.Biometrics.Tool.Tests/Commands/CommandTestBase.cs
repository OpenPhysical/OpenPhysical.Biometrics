using System.Drawing;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Tool.Drawing;

namespace OpenPhysical.Biometrics.Tool.Tests.Commands;

using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
/// Base class for CLI command tests that provides consistent TestConsole setup.
/// </summary>
public abstract class CommandTestBase
{
    /// <summary>Gets the captured console used to inspect command output.</summary>
    protected TestConsole Console = null!;

    /// <summary>Gets the command application configured for each test.</summary>
    protected CommandAppTester App = null!;

    /// <summary>Gets the isolated directory for generated test files.</summary>
    protected string TempDirectory = null!;

    /// <summary>Gets the committed fixture directory.</summary>
    protected string TestDataDirectory = null!;

    /// <summary>
    /// Sets up the test environment with TestConsole and CommandAppTester.
    /// </summary>
    [SetUp]
    public virtual void SetUp()
    {
        // Create temp directory for test outputs
        TempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(TempDirectory);

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

        TestDataDirectory = Path.Combine(
            searchDir.FullName,
            "tests",
            "OpenPhysical.Biometrics.Infrastructure.Tests",
            "TestData",
            "GoldenPiv"
        );

        // Create TestConsole for controlled output
        Console = new TestConsole();
        Console.EmitAnsiSequences = false;
        AnsiConsole.Console = Console; // Plain text output for tests

        // Create the command app with dependency injection
        var services = CreateTestServices();
        App = new CommandAppTester(new DependencyInjectionRegistrar(services));

        ConfigureCommands(App);
    }

    /// <summary>
    /// Cleans up test resources.
    /// </summary>
    [TearDown]
    public virtual void TearDown()
    {
        // Clean up temp directory
        if (Directory.Exists(TempDirectory))
        {
            Directory.Delete(TempDirectory, true);
        }
    }

    /// <summary>
    /// Creates the service collection for testing with TestConsole.
    /// </summary>
    protected virtual ServiceCollection CreateTestServices()
    {
        var services = new ServiceCollection();

        // Create hosting environment for tests
        var hostEnvironment = new SimpleHostEnvironment
        {
            EnvironmentName = "Test",
            ApplicationName = "biotool-test",
            ContentRootPath = AppContext.BaseDirectory,
        };
        services.AddSingleton<IHostEnvironment>(hostEnvironment);

        // Configure logging to suppress output during tests
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Critical); // Only critical errors
        });

        // Register biometric services
        services.AddBiometricServices();

        // Register CBEFF services
        services.AddCbeffServices();
        services.AddCompatibilityCryptographyServices();
        services.AddScoped<ICbeffEncoder, CbeffEncoderService>();
        services.AddScoped<ICbeffDecoder, CbeffDecoderService>();
        services.AddScoped<ICbeffSigner, CbeffSignerService>();
        services.AddScoped<ICbeffVerifier, CbeffVerifierService>();
        services.AddScoped<ICbeffBuilder, CbeffBuilderService>();

        // Register INCITS 385 services
        services.AddSingleton<IIncits385Writer, Incits385WriterService>();

        // Register metadata services
        services.AddMetadataServices();

        // Register certificate store for test environment
        services.AddCertificateStoreForEnvironment(hostEnvironment);

        // Register the TestConsole instance
        services.AddSingleton<IAnsiConsole>(Console);

        // Register commands
        RegisterCommands(services);

        return services;
    }

    /// <summary>
    /// Registers all CLI commands for testing.
    /// </summary>
    protected virtual void RegisterCommands(ServiceCollection services)
    {
        services.AddScoped<InspectCbeffCommand>();
        services.AddScoped<ExtractCbeffCommand>();
        services.AddScoped<CreateCbeffCommand>();
        services.AddScoped<SignCbeffCommand>();
        services.AddScoped<VerifyCbeffCommand>();
        services.AddScoped<ProcessCommand>();
    }

    /// <summary>
    /// Configures the command app with all available commands.
    /// </summary>
    protected virtual void ConfigureCommands(CommandAppTester app)
    {
        app.Configure(config =>
        {
            config.ConfigureConsole(Console);
            config.SetApplicationName("biotool");
            config.ValidateExamples();

            // Process command
            config.AddCommand<ProcessCommand>("process");

            // CBEFF commands
            config.AddBranch(
                "cbeff",
                cbeff =>
                {
                    cbeff.AddCommand<InspectCbeffCommand>("inspect");
                    cbeff.AddCommand<ExtractCbeffCommand>("extract");
                    cbeff.AddCommand<CreateCbeffCommand>("create");
                    cbeff.AddCommand<SignCbeffCommand>("sign");
                    cbeff.AddCommand<VerifyCbeffCommand>("verify");
                }
            );
        });
    }

    /// <summary>
    /// Gets the test image path for a known good face image.
    /// </summary>
    protected string GetTestImagePath()
    {
        var solutionRoot = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (
            solutionRoot != null
            && !File.Exists(Path.Combine(solutionRoot.FullName, "OpenPhysical.Biometrics.sln"))
        )
        {
            solutionRoot = solutionRoot.Parent;
        }

        return Path.Combine(solutionRoot!.FullName, "tests", "sample_images", "generic_guy.jpg");
    }

    /// <summary>Prepares the explicit geometry consumed by the CBEFF packaging tests.</summary>
    protected string GetPreparedPortraitPath()
    {
        var prepared = Path.Combine(TempDirectory, "prepared-portrait.jpg");
        using var image = RgbImage.Load(GetTestImagePath());
        image.Resize(480, 640, fillCrop: true).SaveAsJpeg(prepared, 90);
        return prepared;
    }

    /// <summary>
    /// Creates an empty white image for testing no-face scenarios.
    /// </summary>
    protected string CreateEmptyImage()
    {
        var imagePath = Path.Combine(TempDirectory, "empty.jpg");

        using (var image = new RgbImage(100, 100, new RgbaPixel(255, 255, 255)))
        {
            image.SaveAsJpeg(imagePath, 85);
        }

        return imagePath;
    }
}

/// <summary>
/// Simple hosting environment implementation for console application testing.
/// </summary>
internal sealed class SimpleHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "biotool-test";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
