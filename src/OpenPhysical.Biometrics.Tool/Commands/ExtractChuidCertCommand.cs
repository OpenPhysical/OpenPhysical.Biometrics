namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.X509;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// Command to extract signing certificate from CHUID data.
/// Per FIPS 201-3, the CHUID asymmetric signature field contains a CMS signature
/// with the signing certificate embedded in the certificates field.
/// </summary>
public sealed class ExtractChuidCertCommand : AsyncCommand<ExtractChuidCertCommand.Settings>
{
    private readonly IAnsiConsole _console;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractChuidCertCommand"/> class.
    /// </summary>
    public ExtractChuidCertCommand(IAnsiConsole console)
    {
        _console = console;
    }

    /// <summary>
    /// Settings for the extract CHUID certificate command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to the CHUID file.
        /// </summary>
        [CommandArgument(0, "<chuid-file>")]
        [Description("Path to the CHUID data file (0x5FC1A4.dat)")]
        public string ChuidFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the output directory for extracted certificates.
        /// </summary>
        [CommandOption("--output <directory>")]
        [Description("Output directory for extracted certificates")]
        public string? OutputDirectory { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show detailed analysis.
        /// </summary>
        [CommandOption("--analyze")]
        [Description("Show detailed CHUID structure analysis")]
        public bool ShowAnalysis { get; set; }
    }

    /// <summary>
    /// Executes the extract CHUID certificate command.
    /// </summary>
    public override async Task<int> ExecuteAsync(
        [NotNull] CommandContext context,
        [NotNull] Settings settings
    )
    {
        if (!File.Exists(settings.ChuidFilePath))
        {
            _console.MarkupLine($"[red]CHUID file not found:[/] {settings.ChuidFilePath}");
            return 1;
        }

        try
        {
            var chuidData = await File.ReadAllBytesAsync(settings.ChuidFilePath);
            _console.MarkupLine(
                $"[green]Processing CHUID file:[/] {settings.ChuidFilePath} ({chuidData.Length:N0} bytes)"
            );
            _console.WriteLine();

            // Parse CHUID structure according to SP 800-73-4
            var parseResult = ParseChuidStructure(chuidData);
            if (parseResult.IsFailure)
            {
                _console.MarkupLine(
                    $"[red]Failed to parse CHUID structure:[/] {parseResult.Error}"
                );
                return 1;
            }

            var chuid = parseResult.Value;

            if (settings.ShowAnalysis)
            {
                DisplayChuidAnalysis(chuid);
            }

            // Extract certificate from signature
            if (chuid.AsymmetricSignature != null)
            {
                var certResult = ExtractSigningCertificate(chuid.AsymmetricSignature);
                if (certResult.IsSuccess)
                {
                    var certificate = certResult.Value;
                    _console.MarkupLine($"[green]Extracted signing certificate:[/]");
                    _console.MarkupLine($"  Subject: {certificate.SubjectDN}");
                    _console.MarkupLine($"  Issuer: {certificate.IssuerDN}");
                    _console.MarkupLine($"  Serial: {certificate.SerialNumber}");
                    _console.MarkupLine(
                        $"  Valid: {certificate.NotBefore:yyyy-MM-dd} to {certificate.NotAfter:yyyy-MM-dd}"
                    );

                    // Save certificate if output directory specified
                    if (!string.IsNullOrEmpty(settings.OutputDirectory))
                    {
                        await SaveCertificate(
                            certificate,
                            settings.OutputDirectory,
                            Path.GetFileNameWithoutExtension(settings.ChuidFilePath)
                        );
                    }
                }
                else
                {
                    _console.MarkupLine(
                        $"[red]Failed to extract certificate:[/] {certResult.Error}"
                    );
                    return 1;
                }
            }
            else
            {
                _console.MarkupLine("[yellow]No asymmetric signature found in CHUID[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private Result<ChuidStructure> ParseChuidStructure(byte[] data)
    {
        try
        {
            // CHUID is a TLV structure per SP 800-73-4, Table 9
            var offset = 0;
            byte[]? fascN = null;
            Guid? cardUuid = null;
            DateTime? expirationDate = null;
            byte[]? asymmetricSignature = null;
            byte[]? lrcSignature = null;

            while (offset < data.Length)
            {
                if (offset + 2 > data.Length)
                    break;

                var tag = data[offset];
                var length = data[offset + 1];
                offset += 2;

                if (offset + length > data.Length)
                {
                    return Result.Failure<ChuidStructure>("Invalid TLV length in CHUID data");
                }

                var value = new byte[length];
                Array.Copy(data, offset, value, 0, length);
                offset += length;

                switch (tag)
                {
                    case 0x30: // FASC-N
                        fascN = value;
                        break;
                    case 0x34: // Card UUID
                        if (value.Length == 16)
                        {
                            cardUuid = new Guid(value);
                        }
                        break;
                    case 0x35: // Expiration Date
                        expirationDate = ParseChuidDate(value);
                        break;
                    case 0x3E: // Asymmetric Signature
                        asymmetricSignature = value;
                        break;
                    case 0xFE: // LRC (Error Detection Code)
                        lrcSignature = value;
                        break;
                    // Additional tags can be added as needed
                }
            }

            return Result.Success(
                new ChuidStructure(
                    fascN,
                    cardUuid,
                    expirationDate,
                    asymmetricSignature,
                    lrcSignature
                )
            );
        }
        catch (Exception ex)
        {
            return Result.Failure<ChuidStructure>($"Error parsing CHUID structure: {ex.Message}");
        }
    }

    private Result<X509Certificate> ExtractSigningCertificate(byte[] signatureData)
    {
        try
        {
            // The asymmetric signature is a CMS SignedData structure
            var cmsSignedData = new CmsSignedData(signatureData);

            // Get certificates from the CMS structure
            var certificates = cmsSignedData.GetCertificates();
            var certList = new List<X509Certificate>(certificates.EnumerateMatches(null));

            if (certList.Count == 0)
            {
                return Result.Failure<X509Certificate>("No certificates found in CHUID signature");
            }

            if (certList.Count == 1)
            {
                return Result.Success(certList[0]);
            }

            // If multiple certificates, find the signing certificate
            // Per FIPS 201-3: "If the signature on the biometric was generated with a different key
            // than the signature on the CHUID, the certificates field shall include only a single certificate"
            var signers = cmsSignedData.GetSignerInfos();
            var signerList = signers.Cast<Org.BouncyCastle.Cms.SignerInformation>().ToList();

            if (signerList.Count != 1)
            {
                return Result.Failure<X509Certificate>(
                    $"Expected exactly one signer, found {signerList.Count}"
                );
            }

            var signerInfo = signerList[0];
            var signerId = signerInfo.SignerID;

            // Find the certificate that matches the signer
            foreach (var cert in certList)
            {
                if (signerId.Match(cert))
                {
                    return Result.Success(cert);
                }
            }

            return Result.Failure<X509Certificate>(
                "Could not find signing certificate matching signer ID"
            );
        }
        catch (Exception ex)
        {
            return Result.Failure<X509Certificate>($"Error extracting certificate: {ex.Message}");
        }
    }

    private void DisplayChuidAnalysis(ChuidStructure chuid)
    {
        var tree = new Tree("[yellow]CHUID Structure Analysis[/]");

        if (chuid.FascN != null)
        {
            tree.AddNode($"FASC-N (0x30): {Convert.ToHexString(chuid.FascN)}");
        }

        if (chuid.CardUuid.HasValue)
        {
            tree.AddNode($"Card UUID (0x34): {chuid.CardUuid.Value}");
        }

        if (chuid.ExpirationDate.HasValue)
        {
            tree.AddNode($"Expiration Date (0x35): {chuid.ExpirationDate.Value:yyyy-MM-dd}");
        }

        if (chuid.AsymmetricSignature != null)
        {
            tree.AddNode(
                $"Asymmetric Signature (0x3E): {chuid.AsymmetricSignature.Length:N0} bytes"
            );
        }

        if (chuid.LrcSignature != null)
        {
            tree.AddNode(
                $"LRC/Error Detection Code (0xFE): {Convert.ToHexString(chuid.LrcSignature)}"
            );
        }

        _console.Write(tree);
        _console.WriteLine();
    }

    private async Task SaveCertificate(
        X509Certificate certificate,
        string outputDirectory,
        string baseFileName
    )
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);

            var certFileName = $"{baseFileName}_signer.crt";
            var certPath = Path.Combine(outputDirectory, certFileName);

            var certBytes = certificate.GetEncoded();
            await File.WriteAllBytesAsync(certPath, certBytes);

            _console.MarkupLine($"[green]Saved certificate:[/] {certPath}");
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to save certificate:[/] {ex.Message}");
        }
    }

    private DateTime? ParseChuidDate(byte[] dateBytes)
    {
        try
        {
            // CHUID dates are in YYYYMMDD format (8 bytes)
            if (dateBytes.Length != 8)
                return null;

            var dateString = System.Text.Encoding.ASCII.GetString(dateBytes);
            if (
                DateTime.TryParseExact(
                    dateString,
                    "yyyyMMdd",
                    null,
                    System.Globalization.DateTimeStyles.None,
                    out var date
                )
            )
            {
                return date;
            }
        }
        catch
        {
            // Ignore parse errors
        }
        return null;
    }

    /// <summary>
    /// Represents the parsed structure of a CHUID.
    /// </summary>
    private sealed record ChuidStructure(
        byte[]? FascN,
        Guid? CardUuid,
        DateTime? ExpirationDate,
        byte[]? AsymmetricSignature,
        byte[]? LrcSignature
    );
}
