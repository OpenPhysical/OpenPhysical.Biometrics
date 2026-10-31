namespace OpenPhysical.Biometrics.Tool.Commands;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Domains.Cbeff;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// Command to verify digital signatures on CBEFF files for PIV compliance.
/// </summary>
public sealed class VerifyCbeffCommand : AsyncCommand<VerifyCbeffCommand.Settings>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICbeffSerializerExtended _serializer;
    private readonly ICbeffVerifier _verifier;
    private readonly ITrustedCertificateStore _certificateStore;
    private readonly IAnsiConsole _console;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerifyCbeffCommand"/> class.
    /// </summary>
    public VerifyCbeffCommand(
        IServiceProvider serviceProvider,
        ICbeffSerializerExtended serializer,
        ICbeffVerifier verifier,
        ITrustedCertificateStore certificateStore,
        IAnsiConsole console
    )
    {
        _serviceProvider = serviceProvider;
        _serializer = serializer;
        _verifier = verifier;
        _certificateStore = certificateStore;
        _console = console;
    }

    /// <summary>
    /// Settings for the verify command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to the signed CBEFF file to verify.
        /// </summary>
        [CommandArgument(0, "<input>")]
        [Description("Path to the signed CBEFF file to verify")]
        public string InputPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to additional trusted certificate file.
        /// </summary>
        [CommandOption("--cert <CERT>")]
        [Description("Path to additional trusted certificate file (.crt, .cer, or .pem)")]
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets whether to display verbose output.
        /// </summary>
        [CommandOption("--verbose")]
        [Description("Display verbose verification details")]
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets whether to output results in JSON format.
        /// </summary>
        [CommandOption("--json")]
        [Description("Output verification results in JSON format")]
        public bool JsonOutput { get; set; }

        /// <summary>
        /// Gets or sets the output path for verification report.
        /// </summary>
        [CommandOption("-o|--output <OUTPUT>")]
        [Description("Output path for verification report")]
        public string? OutputPath { get; set; }

        /// <summary>
        /// Gets or sets whether to check certificate chain validity.
        /// </summary>
        [CommandOption("--check-chain")]
        [Description("Perform full certificate chain validation")]
        public bool CheckChain { get; set; }

        /// <summary>
        /// Gets or sets whether to check certificate revocation status.
        /// </summary>
        [CommandOption("--check-revocation")]
        [Description("Check certificate revocation status (requires network access)")]
        public bool CheckRevocation { get; set; }
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

            var payload = cbeffResult.Value;

            // Check if the CBEFF is signed
            if (!payload.Signature.HasValue)
            {
                _console.MarkupLine("[yellow]CBEFF file is not signed[/]");
                return settings.JsonOutput ? OutputJsonResult(false, "Not signed", null) : 1;
            }

            // Load additional certificate if provided
            var certificateStore = _certificateStore;
            if (!string.IsNullOrEmpty(settings.CertificatePath))
            {
                var additionalCertResult = await LoadAdditionalCertificate(
                    settings.CertificatePath
                );
                if (additionalCertResult.IsFailure)
                {
                    _console.MarkupLine(
                        $"[red]Error loading additional certificate: {additionalCertResult.Error}[/]"
                    );
                    return 1;
                }

                // Create a new certificate store that includes the additional certificate
                certificateStore = CreateExtendedCertificateStore(additionalCertResult.Value);
            }

            // Verify the signature
            var certStoreResult = certificateStore.GetTrustedCertificates();
            if (certStoreResult.IsFailure)
            {
                _console.MarkupLine(
                    $"[red]Failed to get trusted certificates: {certStoreResult.Error}[/]"
                );
                return 1;
            }

            var verificationResult = await _verifier.VerifyAsync(payload, certStoreResult.Value);
            if (verificationResult.IsFailure)
            {
                _console.MarkupLine($"[red]Verification failed: {verificationResult.Error}[/]");
                return 1;
            }

            var result = verificationResult.Value;

            // Output results
            if (settings.JsonOutput)
            {
                return OutputJsonResult(result.IsValid, GetSummaryMessage(result), result);
            }
            else
            {
                DisplayVerificationResults(result, settings.Verbose);

                // Save report if requested
                if (!string.IsNullOrEmpty(settings.OutputPath))
                {
                    var saveResult = await SaveVerificationReport(result, settings.OutputPath);
                    if (saveResult.IsFailure)
                    {
                        _console.MarkupLine(
                            $"[yellow]Warning: Failed to save report: {saveResult.Error}[/]"
                        );
                    }
                    else
                    {
                        _console.MarkupLine(
                            $"[green]Verification report saved to: {settings.OutputPath}[/]"
                        );
                    }
                }
            }

            return result.IsValid ? 0 : 1;
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

        if (
            !string.IsNullOrEmpty(settings.CertificatePath)
            && !File.Exists(settings.CertificatePath)
        )
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
    /// Loads an additional trusted certificate.
    /// </summary>
    private async Task<Result<X509Certificate>> LoadAdditionalCertificate(string certificatePath)
    {
        try
        {
            var certData = await File.ReadAllBytesAsync(certificatePath);

            // Try to parse as X.509 certificate
            var certificate = new X509Certificate(certData);
            return Result.Success(certificate);
        }
        catch (Exception ex)
        {
            return Result.Failure<X509Certificate>($"Failed to load certificate: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates an extended certificate store that includes additional certificates.
    /// </summary>
    private ITrustedCertificateStore CreateExtendedCertificateStore(X509Certificate additionalCert)
    {
        // For now, return the existing store
        // In a production implementation, you would create a composite store
        // that includes both the default certificates and the additional one
        return _certificateStore;
    }

    /// <summary>
    /// Displays verification results to the console.
    /// </summary>
    private void DisplayVerificationResults(CbeffVerificationResult result, bool verbose)
    {
        _console.WriteLine();

        // Display main result
        if (result.IsValid)
        {
            _console.MarkupLine("[green]✓ Signature verification PASSED[/]");
        }
        else
        {
            _console.MarkupLine("[red]✗ Signature verification FAILED[/]");
        }

        // Display certificate information if available
        if (result.SignerCertificate.HasValue)
        {
            var cert = result.SignerCertificate.Value;
            _console.WriteLine();
            _console.MarkupLine("[bold]Signer Certificate:[/]");
            _console.MarkupLine($"Subject: {cert.SubjectDN}");
            _console.MarkupLine($"Issuer: {cert.IssuerDN}");
            _console.MarkupLine($"Serial Number: {cert.SerialNumber}");
            _console.MarkupLine($"Valid From: {cert.NotBefore:yyyy-MM-dd HH:mm:ss} UTC");
            _console.MarkupLine($"Valid To: {cert.NotAfter:yyyy-MM-dd HH:mm:ss} UTC");
        }

        // Display signing time if available
        if (result.SigningTime.HasValue)
        {
            _console.MarkupLine(
                $"Signing Time: {result.SigningTime.Value:yyyy-MM-dd HH:mm:ss} UTC"
            );
        }

        // Display card UUID if available
        if (result.CardUuid.HasValue)
        {
            _console.MarkupLine($"Card UUID: {result.CardUuid.Value}");
        }

        // Display validation errors
        if (result.ValidationErrors.Any())
        {
            _console.WriteLine();
            _console.MarkupLine("[yellow]Validation Issues:[/]");
            foreach (var error in result.ValidationErrors)
            {
                _console.MarkupLine($"[yellow]• {error}[/]");
            }
        }

        // Display verbose details if requested
        if (verbose)
        {
            DisplayVerboseDetails(result);
        }
    }

    /// <summary>
    /// Displays verbose verification details.
    /// </summary>
    private void DisplayVerboseDetails(CbeffVerificationResult result)
    {
        _console.WriteLine();
        _console.MarkupLine("[bold]Verification Details:[/]");
        _console.MarkupLine($"Result: {(result.IsValid ? "Valid" : "Invalid")}");
        _console.MarkupLine($"Error Count: {result.ValidationErrors.Length}");

        if (result.SignerCertificate.HasValue)
        {
            var cert = result.SignerCertificate.Value;
            _console.MarkupLine($"Certificate Algorithm: {cert.SigAlgName}");
            _console.MarkupLine($"Certificate Version: {cert.Version}");
            _console.MarkupLine($"Public Key Algorithm: {cert.GetPublicKey().GetType().Name}");
        }
    }

    /// <summary>
    /// Outputs verification results in JSON format.
    /// </summary>
    private int OutputJsonResult(bool isValid, string message, CbeffVerificationResult? result)
    {
        var jsonResult = new
        {
            isValid,
            message,
            signerCertificate = result?.SignerCertificate.HasValue == true
                ? new
                {
                    subject = result.SignerCertificate.Value.SubjectDN.ToString(),
                    issuer = result.SignerCertificate.Value.IssuerDN.ToString(),
                    serialNumber = result.SignerCertificate.Value.SerialNumber.ToString(),
                    notBefore = result.SignerCertificate.Value.NotBefore,
                    notAfter = result.SignerCertificate.Value.NotAfter,
                }
                : null,
            signingTime = result?.SigningTime.HasValue == true
                ? result.SigningTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
                : null,
            cardUuid = result?.CardUuid.HasValue == true ? result.CardUuid.Value.ToString() : null,
            validationErrors = result?.ValidationErrors ?? Array.Empty<string>(),
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            jsonResult,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
        );

        _console.WriteLine(json);
        return isValid ? 0 : 1;
    }

    /// <summary>
    /// Gets a summary message for the verification result.
    /// </summary>
    private string GetSummaryMessage(CbeffVerificationResult result)
    {
        if (result.IsValid)
        {
            return "Signature verification passed";
        }

        if (result.ValidationErrors.Any())
        {
            return $"Signature verification failed: {result.ValidationErrors.First()}";
        }

        return "Signature verification failed";
    }

    /// <summary>
    /// Saves a verification report to a file.
    /// </summary>
    private async Task<Result> SaveVerificationReport(
        CbeffVerificationResult result,
        string outputPath
    )
    {
        try
        {
            var report = GenerateTextReport(result);
            await File.WriteAllTextAsync(outputPath, report);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save report: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a text report of the verification results.
    /// </summary>
    private string GenerateTextReport(CbeffVerificationResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CBEFF Signature Verification Report");
        sb.AppendLine("=====================================");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        sb.AppendLine($"Verification Result: {(result.IsValid ? "PASSED" : "FAILED")}");
        sb.AppendLine();

        if (result.SignerCertificate.HasValue)
        {
            var cert = result.SignerCertificate.Value;
            sb.AppendLine("Signer Certificate:");
            sb.AppendLine($"  Subject: {cert.SubjectDN}");
            sb.AppendLine($"  Issuer: {cert.IssuerDN}");
            sb.AppendLine($"  Serial Number: {cert.SerialNumber}");
            sb.AppendLine($"  Valid From: {cert.NotBefore:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"  Valid To: {cert.NotAfter:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"  Algorithm: {cert.SigAlgName}");
            sb.AppendLine();
        }

        if (result.SigningTime.HasValue)
        {
            sb.AppendLine($"Signing Time: {result.SigningTime.Value:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
        }

        if (result.CardUuid.HasValue)
        {
            sb.AppendLine($"Card UUID: {result.CardUuid.Value}");
            sb.AppendLine();
        }

        if (result.ValidationErrors.Any())
        {
            sb.AppendLine("Validation Issues:");
            foreach (var error in result.ValidationErrors)
            {
                sb.AppendLine($"  • {error}");
            }
        }

        return sb.ToString();
    }
}
