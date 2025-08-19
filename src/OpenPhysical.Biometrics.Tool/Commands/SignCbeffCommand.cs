namespace OpenPhysical.Biometrics.Tool.Commands;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Domains.Cbeff;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Crypto;
using CSharpFunctionalExtensions;

/// <summary>
/// Command to sign CBEFF files with digital signatures for PIV compliance.
/// </summary>
public sealed class SignCbeffCommand : AsyncCommand<SignCbeffCommand.Settings>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICbeffSerializerExtended _serializer;
    private readonly ICbeffSigner _signer;
    private readonly IAnsiConsole _console;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignCbeffCommand"/> class.
    /// </summary>
    public SignCbeffCommand(
        IServiceProvider serviceProvider, 
        ICbeffSerializerExtended serializer,
        ICbeffSigner signer,
        IAnsiConsole console)
    {
        _serviceProvider = serviceProvider;
        _serializer = serializer;
        _signer = signer;
        _console = console;
    }

    /// <summary>
    /// Settings for the sign command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to the CBEFF file to sign.
        /// </summary>
        [CommandArgument(0, "<input>")]
        [Description("Path to the CBEFF file to sign")]
        public string InputPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the signing certificate file (.p12 or .pfx).
        /// </summary>
        [CommandOption("--cert <CERT>")]
        [Description("Path to the signing certificate file (.p12 or .pfx)")]
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the password for the certificate file.
        /// </summary>
        [CommandOption("--password <PASSWORD>")]
        [Description("Password for the certificate file")]
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the output path for the signed CBEFF file.
        /// </summary>
        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Output path for signed CBEFF file (default: <input>.signed)")]
        public string? OutputPath { get; set; }

        /// <summary>
        /// Gets or sets the card UUID to include in the signature.
        /// </summary>
        [CommandOption("--card-uuid <UUID>")]
        [Description("Card UUID to include in the signature")]
        public string? CardUuid { get; set; }

        /// <summary>
        /// Gets or sets whether to overwrite existing output files.
        /// </summary>
        [CommandOption("--force")]
        [Description("Overwrite output file if it exists")]
        public bool Force { get; set; }

        /// <summary>
        /// Gets or sets whether to display verbose output.
        /// </summary>
        [CommandOption("--verbose")]
        [Description("Display verbose output")]
        public bool Verbose { get; set; }
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            // Validate inputs
            var validationResult = ValidateInputs(settings);
            if (validationResult.IsFailure)
            {
                _console.MarkupLine($"[red]Error: {validationResult.Error}[/]");
                return 1;
            }

            // Load and parse the CBEFF file
            var cbeffResult = await LoadCbeffFile(settings.InputPath);
            if (cbeffResult.IsFailure)
            {
                _console.MarkupLine($"[red]Error loading CBEFF file: {cbeffResult.Error}[/]");
                return 1;
            }

            // Load signing certificate and private key
            var certResult = LoadSigningCertificate(settings);
            if (certResult.IsFailure)
            {
                _console.MarkupLine($"[red]Error loading certificate: {certResult.Error}[/]");
                return 1;
            }

            var (certificate, privateKey) = certResult.Value;

            // Parse card UUID if provided
            Guid? cardUuid = null;
            if (!string.IsNullOrEmpty(settings.CardUuid))
            {
                if (!Guid.TryParse(settings.CardUuid, out var uuid))
                {
                    _console.MarkupLine($"[red]Error: Invalid card UUID format: {settings.CardUuid}[/]");
                    return 1;
                }
                cardUuid = uuid;
            }

            // Sign the CBEFF payload  
            var maybeCardUuid = cardUuid.HasValue ? Maybe<Guid>.From(cardUuid.Value) : Maybe<Guid>.None;
            var signResult = await _signer.SignAsync(cbeffResult.Value, certificate, privateKey, maybeCardUuid);
            if (signResult.IsFailure)
            {
                _console.MarkupLine($"[red]Error signing CBEFF: {signResult.Error}[/]");
                return 1;
            }

            // Determine output path
            var outputPath = settings.OutputPath ?? $"{settings.InputPath}.signed";
            
            // Check if output file exists
            if (File.Exists(outputPath) && !settings.Force)
            {
                _console.MarkupLine($"[yellow]Output file already exists: {outputPath}[/]");
                _console.MarkupLine("[yellow]Use --force to overwrite[/]");
                return 1;
            }

            // Save the signed CBEFF file
            var saveResult = await SaveSignedCbeff(signResult.Value, outputPath);
            if (saveResult.IsFailure)
            {
                _console.MarkupLine($"[red]Error saving signed CBEFF: {saveResult.Error}[/]");
                return 1;
            }

            // Display success message
            _console.MarkupLine($"[green]✓ Successfully signed CBEFF file[/]");
            _console.MarkupLine($"[green]Output: {outputPath}[/]");

            if (settings.Verbose)
            {
                DisplaySigningDetails(certificate, cardUuid);
            }

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Unexpected error: {ex.Message}[/]");
            if (settings.Verbose)
            {
                _console.WriteException(ex);
            }
            return 1;
        }
    }

    /// <summary>
    /// Validates the command inputs.
    /// </summary>
    private Result ValidateInputs(Settings settings)
    {
        if (string.IsNullOrEmpty(settings.InputPath))
        {
            return Result.Failure("Input path is required");
        }

        if (!File.Exists(settings.InputPath))
        {
            return Result.Failure($"Input file not found: {settings.InputPath}");
        }

        if (string.IsNullOrEmpty(settings.CertificatePath))
        {
            return Result.Failure("Certificate path is required (--cert)");
        }

        if (!File.Exists(settings.CertificatePath))
        {
            return Result.Failure($"Certificate file not found: {settings.CertificatePath}");
        }

        return Result.Success();
    }

    /// <summary>
    /// Loads and parses the CBEFF file.
    /// </summary>
    private async Task<Result<CbeffPayload>> LoadCbeffFile(string filePath)
    {
        try
        {
            var data = await File.ReadAllBytesAsync(filePath);
            return _serializer.Deserialize(data);
        }
        catch (Exception ex)
        {
            return Result.Failure<CbeffPayload>($"Failed to load CBEFF file: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the signing certificate and private key.
    /// </summary>
    private Result<(Org.BouncyCastle.X509.X509Certificate certificate, AsymmetricKeyParameter privateKey)> LoadSigningCertificate(Settings settings)
    {
        try
        {
            // Read the certificate file
            var certData = File.ReadAllBytes(settings.CertificatePath!);
            
            // Parse as PKCS#12
            var pkcs12 = new Pkcs12StoreBuilder().Build();
            
            // Use password if provided, otherwise empty string
            var password = settings.Password?.ToCharArray() ?? Array.Empty<char>();
            
            using var stream = new MemoryStream(certData);
            pkcs12.Load(stream, password);

            // Find the certificate and private key
            Org.BouncyCastle.X509.X509Certificate? certificate = null;
            AsymmetricKeyParameter? privateKey = null;

            foreach (string alias in pkcs12.Aliases)
            {
                if (pkcs12.IsKeyEntry(alias))
                {
                    privateKey = pkcs12.GetKey(alias).Key;
                    var certChain = pkcs12.GetCertificateChain(alias);
                    if (certChain != null && certChain.Length > 0)
                    {
                        certificate = certChain[0].Certificate;
                        break;
                    }
                }
            }

            if (certificate == null)
            {
                return Result.Failure<(Org.BouncyCastle.X509.X509Certificate, AsymmetricKeyParameter)>("No certificate found in PKCS#12 file");
            }

            if (privateKey == null)
            {
                return Result.Failure<(Org.BouncyCastle.X509.X509Certificate, AsymmetricKeyParameter)>("No private key found in PKCS#12 file");
            }

            return Result.Success((certificate, privateKey));
        }
        catch (Exception ex)
        {
            return Result.Failure<(Org.BouncyCastle.X509.X509Certificate, AsymmetricKeyParameter)>($"Failed to load certificate: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the signed CBEFF payload to a file.
    /// </summary>
    private async Task<Result> SaveSignedCbeff(CbeffPayload signedPayload, string outputPath)
    {
        try
        {
            var serializedResult = _serializer.Serialize(signedPayload);
            if (serializedResult.IsFailure)
                return Result.Failure($"Failed to serialize signed CBEFF: {serializedResult.Error}");
                
            await File.WriteAllBytesAsync(outputPath, serializedResult.Value.ToArray());
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save signed CBEFF: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays signing details in verbose mode.
    /// </summary>
    private void DisplaySigningDetails(Org.BouncyCastle.X509.X509Certificate certificate, Guid? cardUuid)
    {
        _console.WriteLine();
        _console.MarkupLine("[bold]Signing Details:[/]");
        _console.MarkupLine($"Certificate Subject: {certificate.SubjectDN}");
        _console.MarkupLine($"Certificate Issuer: {certificate.IssuerDN}");
        _console.MarkupLine($"Certificate Serial: {certificate.SerialNumber}");
        _console.MarkupLine($"Certificate Valid From: {certificate.NotBefore:yyyy-MM-dd HH:mm:ss} UTC");
        _console.MarkupLine($"Certificate Valid To: {certificate.NotAfter:yyyy-MM-dd HH:mm:ss} UTC");
        
        if (cardUuid.HasValue)
        {
            _console.MarkupLine($"Card UUID: {cardUuid.Value}");
        }
    }
}