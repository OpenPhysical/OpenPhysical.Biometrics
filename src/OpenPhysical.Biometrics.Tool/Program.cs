using OpenPhysical.Biometrics.Tool.Commands;
using OpenPhysical.Biometrics.Infrastructure.Extensions;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Standards.Incits385;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Standards.Incits385;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Extensions.DependencyInjection;

// Configure services
var services = new ServiceCollection();

// Create hosting environment
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? 
                  Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                  "Production";

var hostEnvironment = new SimpleHostEnvironment
{
    EnvironmentName = environment,
    ApplicationName = "biotool",
    ContentRootPath = AppContext.BaseDirectory
};

services.AddSingleton<IHostEnvironment>(hostEnvironment);

// Configure logging - check for --debug flag in args
var hasDebugFlag = args.Contains("--debug");

// Configure logging
services.AddLogging(builder =>
{
    // Clear existing providers first
    builder.ClearProviders();

    // Set minimum level based on debug flag or cross-validate command
    var hasCrossValidateFlag = args.Contains("--cross-validate");
    if (hasDebugFlag || hasCrossValidateFlag)
    {
        builder.SetMinimumLevel(LogLevel.Debug);
    }
    else
    {
        builder.SetMinimumLevel(LogLevel.Warning);
    }

    // Use simple console logging for now
    builder.AddSimpleConsole(options =>
    {
        options.IncludeScopes = false;
        options.SingleLine = true;
        options.TimestampFormat = (hasDebugFlag || hasCrossValidateFlag) ? "HH:mm:ss " : null;
    });
});

// Register biometrics services - functional approach
services.AddBiometricServices();

// Register biometric format handlers
services.AddBiometricFormats();

// Register CBEFF services
services.AddCbeffServices();
services.AddScoped<ICbeffEncoder, CbeffEncoderService>();
services.AddScoped<ICbeffDecoder, CbeffDecoderService>();
services.AddScoped<ICbeffSigner, CbeffSignerService>();
services.AddScoped<ICbeffVerifier, CbeffVerifierService>();
services.AddScoped<ICbeffBuilder, CbeffBuilderService>();

// Register INCITS 385 services
services.AddSingleton<IIncits385Writer, Incits385WriterService>();


// Register metadata services
services.AddMetadataServices();

// Register certificate store based on environment
services.AddCertificateStoreForEnvironment(hostEnvironment);

// Register console - check for test/CI environment
var isTestEnvironment = Environment.GetEnvironmentVariable("CI") == "true" ||
                        Environment.GetEnvironmentVariable("TERM") == "dumb" ||
                        Environment.GetEnvironmentVariable("NO_COLOR") == "1" ||
                        args.Contains("--test-mode");

if (isTestEnvironment)
{
    // Use plain console for tests and CI environments
    var plainConsole = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.No,
        ColorSystem = ColorSystemSupport.NoColors,
        Interactive = InteractionSupport.No,
        Out = new AnsiConsoleOutput(Console.Out)
    });
    services.AddSingleton<IAnsiConsole>(plainConsole);
}
else
{
    services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
}

// Register CLI commands
services.AddScoped<InspectCbeffCommand>();
services.AddScoped<ExtractCbeffCommand>();
services.AddScoped<CreateCbeffCommand>();
services.AddScoped<SignCbeffCommand>();
services.AddScoped<VerifyCbeffCommand>();
services.AddScoped<ExtractChuidCertCommand>();
services.AddScoped<ProcessCommand>();

// Build the service provider and initialize the biometric registry
var serviceProvider = services.BuildServiceProvider();
serviceProvider.InitializeBiometricRegistry();

// Create the CLI app with dependency injection
using var registrar = new DependencyInjectionRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("biotool");
    config.SetApplicationVersion(
        typeof(Program).Assembly.GetName().Version?.ToString() ?? "Unknown"
    );
    config.ValidateExamples();

    // PIV Commands - single clean command with functional implementation
    config
        .AddCommand<ProcessCommand>("process")
        .WithDescription("Process images for PIV compliance (supports JP2, JPEG, PNG, TIFF output)")
        .WithExample("process", "photo.jpg")
        .WithExample("process", "photo.jpg", "--output", "result.jp2")
        .WithExample("process", "photo.jpg", "--verbose")
        .WithExample("process", "photo.jpg", "--no-resize", "--format", "jpeg", "--no-roi")
        .WithExample("process", "photo.jpg", "--no-resize", "--format", "jp2", "--no-roi")
        .WithExample("process", "photo.jpg", "--format", "png", "--no-roi")
        .WithExample("process", "photo.jpg", "--format", "tiff", "--no-roi")
        .WithExample("process", "photo.jpg", "--quality-gate", "0.8")
        .WithExample("process", "photo.jpg", "--quality-gate", "0.7", "--quality-report");

    /*
    // ROI Commands - visualize facial region for JPEG 2000 encoding
    config
        .AddCommand<RoiCommand>("roi")
        .WithDescription("Visualize facial ROI Inner Region for JPEG 2000 encoding")
        .WithExample("roi", "photo.jpg")
        .WithExample("roi", "photo.jpg", "--output", "roi_visual.jpg", "--show-landmarks")
        .WithExample("roi", "photo.jpg", "--stroke-width", "5", "--verbose");

    // Quality Commands - ISO/IEC 19794-5 quality assessment
    config
        .AddCommand<QualityCommand>("quality")
        .WithDescription("Assess facial image quality according to ISO/IEC 19794-5")
        .WithExample("quality", "--input", "photo.jpg")
        .WithExample("quality", "--input", "photo.jpg", "--standard", "piv", "--format", "json")
        .WithExample("quality", "--input", "photo.jpg", "--threshold", "0.8", "--strict")
        .WithExample("quality", "--input", "photo.jpg", "--format", "detailed", "--visual");

    // Interactive Commands - Step-by-step validation mode
    config
        .AddCommand<InteractiveCommand>("interactive")
        .WithDescription("Interactive validation mode with step-by-step guidance")
        .WithExample("interactive")
        .WithExample("interactive", "--directory", "./images");

    // Universal CLI Commands - Analysis without processing
    config
        .AddCommand<ValidateCommand>("validate")
        .WithDescription("Quick compliance validation - pass/fail without processing")
        .WithExample("validate", "photo.jpg")
        .WithExample("validate", "photo.jpg", "--standard", "icao")
        .WithExample("validate", "photo.jpg", "--standard", "piv", "--detailed")
        .WithExample("validate", "photo.jpg", "--min-confidence", "0.9");

    // Dataset Commands - Batch processing and validation
    config
        .AddBranch("dataset", dataset =>
        {
            dataset.SetDescription("Dataset validation and batch processing commands");
            
            dataset.AddCommand<DatasetValidateCommand>("validate")
                .WithDescription("Validate and process an entire dataset")
                .WithExample("dataset", "validate", "./dataset/SFHQ-T2I", "--standard", "piv", "--relaxed-crop")
                .WithExample("dataset", "validate", "./dataset/wider-face", "--filter-quality", "--min-face-size", "50")
                .WithExample("dataset", "validate", "./dataset/icao-synthetic", "--standard", "icao", "--interactive");
                
            dataset.AddCommand<DatasetReviewCommand>("review")
                .WithDescription("Review failed images from dataset validation")
                .WithExample("dataset", "review", "./processed_dataset/results.json")
                .WithExample("dataset", "review", "./processed_dataset/results.json", "--min-sharpness", "0.4", "--max-sharpness", "0.7")
                .WithExample("dataset", "review", "./processed_dataset/results.json", "--reason", "LowSharpness", "--output", "override.txt");
        });
    */
    
    // CBEFF Commands - Inspect and work with CBEFF data
    config
        .AddBranch("cbeff", cbeff =>
        {
            cbeff.SetDescription("CBEFF biometric data commands");
            
            cbeff.AddCommand<InspectCbeffCommand>("inspect")
                .WithDescription("Inspect CBEFF biometric data object files")
                .WithExample("cbeff", "inspect", "facial_image.bin")
                .WithExample("cbeff", "inspect", "facial_image.bin", "--hex")
                .WithExample("cbeff", "inspect", "fingerprint.bin", "--extract", "minutiae.dat")
                .WithExample("cbeff", "inspect", "signed_cbeff.bin", "--cert", "signer.crt");
                
            cbeff.AddCommand<ExtractCbeffCommand>("extract")
                .WithDescription("Extract images from CBEFF containers")
                .WithExample("cbeff", "extract", "facial_image.cbeff")
                .WithExample("cbeff", "extract", "facial_image.cbeff", "--output", "face.jpg")
                .WithExample("cbeff", "extract", "facial_image.cbeff", "--save-metadata");
                
            cbeff.AddCommand<CreateCbeffCommand>("create")
                .WithDescription("Create CBEFF containers from facial images")
                .WithExample("cbeff", "create", "face.jpg")
                .WithExample("cbeff", "create", "face.jp2", "--standard", "PIV", "--quality", "90")
                .WithExample("cbeff", "create", "face.jpg", "--piv-wrap", "--fascn", "0123456789ABCDEF...")
                .WithExample("cbeff", "create", "face.jpg", "--gender", "M", "--eye-color", "brown");
                
            cbeff.AddCommand<SignCbeffCommand>("sign")
                .WithDescription("Sign CBEFF files with digital signatures")
                .WithExample("cbeff", "sign", "facial_image.cbeff", "--cert", "signer.p12", "--password", "secret")
                .WithExample("cbeff", "sign", "facial_image.cbeff", "--cert", "signer.p12", "--output", "signed.cbeff")
                .WithExample("cbeff", "sign", "facial_image.cbeff", "--cert", "signer.p12", "--card-uuid", "12345678-1234-5678-9abc-123456789abc");
                
            cbeff.AddCommand<VerifyCbeffCommand>("verify")
                .WithDescription("Verify digital signatures on CBEFF files")
                .WithExample("cbeff", "verify", "signed_facial_image.cbeff")
                .WithExample("cbeff", "verify", "signed_facial_image.cbeff", "--cert", "trusted.crt")
                .WithExample("cbeff", "verify", "signed_facial_image.cbeff", "--verbose", "--json")
                .WithExample("cbeff", "verify", "signed_facial_image.cbeff", "--output", "verification_report.txt");
        });

    // PIV Commands - Extract certificates from PIV data objects
    config
        .AddBranch("piv", piv =>
        {
            piv.SetDescription("PIV data object commands");
            
            piv.AddCommand<ExtractChuidCertCommand>("extract-chuid-cert")
                .WithDescription("Extract signing certificate from CHUID data (0x5FC1A4)")
                .WithExample("piv", "extract-chuid-cert", "5FC1A4.dat")
                .WithExample("piv", "extract-chuid-cert", "5FC1A4.dat", "--output", "./certs", "--analyze")
                .WithExample("piv", "extract-chuid-cert", "golden_piv/01_Golden_PIV/5FC1A4.dat", "--analyze");
        });

    /*
    // Test Commands - Automated testing and calibration
    config
        .AddBranch("test", test =>
        {
            test.SetDescription("Automated testing and calibration tools");
            
            test.AddCommand<TestBlurCommand>("blur")
                .WithDescription("Generate blur progressions for testing")
                .WithExample("test", "blur", "tests/sample_images/*.jpg")
                .WithExample("test", "blur", "tests/sample_images/*.jpg", "--levels", "0,0.3,0.5,0.63,0.7,1.0", "--measure-baseline");
                
            test.AddCommand<TestSharpnessCommand>("sharpness")
                .WithDescription("Test sharpness calibration on blur progressions")
                .WithExample("test", "sharpness", "--threshold", "0.7")
                .WithExample("test", "sharpness", "--input", ".faceoffx-test/blur-progressions", "--output", "sharpness-results.json");
                
            test.AddCommand<TestReportCommand>("report")
                .WithDescription("Generate HTML reports from test results")
                .WithExample("test", "report", "--type", "sharpness-calibration")
                .WithExample("test", "report", "--input", "sharpness-results.json", "--open");
                
            test.AddCommand<TestCleanCommand>("clean")
                .WithDescription("Clean up test artifacts")
                .WithExample("test", "clean", "--all")
                .WithExample("test", "clean", "--blur", "--results", "--force");
        });
    */
});

// Run the CLI app
try
{
    return await app.RunAsync(args);
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
    return 1;
}

// Simple hosting environment implementation for console applications
internal sealed class SimpleHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "biotool";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
