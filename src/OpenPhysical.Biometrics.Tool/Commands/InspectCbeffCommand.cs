namespace OpenPhysical.Biometrics.Tool.Commands;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Biometrics.Core.Services;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using OpenPhysical.Domains.Cbeff;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Asn1.Cms;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Utilities.Collections;
using CSharpFunctionalExtensions;

/// <summary>
/// Command to inspect CBEFF files and display their structure with detailed CMS analysis.
/// </summary>
public sealed class InspectCbeffCommand : AsyncCommand<InspectCbeffCommand.Settings>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICbeffSerializerExtended _serializer;
    private readonly IAnsiConsole _console;
    private byte[]? _headerFascN = null;
    private byte[]? _rawCbeffData = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="InspectCbeffCommand"/> class.
    /// </summary>
    public InspectCbeffCommand(IServiceProvider serviceProvider, ICbeffSerializerExtended serializer, IAnsiConsole console)
    {
        _serviceProvider = serviceProvider;
        _serializer = serializer;
        _console = console;
    }

    /// <summary>
    /// Settings for the inspect command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to the CBEFF file to inspect.
        /// </summary>
        [CommandArgument(0, "<file>")]
        [Description("Path to the CBEFF file to inspect")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to show hex dumps.
        /// </summary>
        [CommandOption("--hex")]
        [Description("Show hex dumps of binary data")]
        public bool ShowHex { get; set; }

        /// <summary>
        /// Gets or sets the output path for extracted data.
        /// </summary>
        [CommandOption("--extract <path>")]
        [Description("Extract biometric data to specified path")]
        public string? ExtractPath { get; set; }

        /// <summary>
        /// Gets or sets the certificate path for signature verification.
        /// </summary>
        [CommandOption("--cert <path>")]
        [Description("Certificate file for signature verification")]
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the path to the CHUID file for cross-validation.
        /// </summary>
        [CommandOption("--chuid <path>")]
        [Description("Path to CHUID file (5FC1A4.dat) for PIV cross-validation")]
        public string? ChuidFilePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable PIV cross-validation.
        /// </summary>
        [CommandOption("--cross-validate")]
        [Description("Enable comprehensive PIV CBEFF-CHUID cross-validation")]
        public bool EnableCrossValidation { get; set; }
    }

    /// <summary>
    /// Executes the inspect command.
    /// </summary>
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        if (!File.Exists(settings.FilePath))
        {
            _console.MarkupLine($"[red]File not found:[/] {settings.FilePath}");
            return 1;
        }

        try
        {
            var data = await File.ReadAllBytesAsync(settings.FilePath);
            _console.MarkupLine($"[green]Inspecting CBEFF file:[/] {settings.FilePath} ({data.Length:N0} bytes)");
            _console.WriteLine();

            // Store raw data for digest verification
            _rawCbeffData = data;

            var result = _serializer.DeserializeAutoExtended(data);
            if (result.IsFailure)
            {
                _console.MarkupLine($"[red]Failed to parse CBEFF:[/] {result.Error}");
                return 1;
            }

            var payload = result.Value;
            DisplayCbeffStructure(payload, settings);

            // Extract data if requested
            if (!string.IsNullOrEmpty(settings.ExtractPath))
            {
                await ExtractBiometricData(payload, settings);
            }

            // Verify signature if present
            if (settings.EnableCrossValidation)
            {
                await PerformCrossValidation(payload, settings);
            }
            else
            {
                await VerifySignature(payload, settings);
            }

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private void DisplayCbeffStructure(CbeffPayloadExtended payload, Settings settings)
    {
        var tree = new Tree("[yellow]CBEFF Structure[/]");

        // Header information
        var headerNode = tree.AddNode("[green]CBEFF Header[/]");
        var header = payload.Header;

        headerNode.AddNode($"Patron Header Version: {header.PatronHeaderVersion:X2}");
        headerNode.AddNode($"SBH Security Options: {header.SbhSecurityOptions:X2}");
        headerNode.AddNode($"BDB Format Owner: {header.BdbFormatOwner:X4} ({GetFormatOwnerName(header.BdbFormatOwner)})");
        headerNode.AddNode($"BDB Format Type: {header.BdbFormatType:X4} ({GetFormatTypeName(header.BdbFormatType)})");
        headerNode.AddNode($"Creation Date: {header.CreationDate:yyyy-MM-dd HH:mm:ss}");
        headerNode.AddNode($"Validity Not Before: {header.ValidityNotBefore:yyyy-MM-dd HH:mm:ss}");
        headerNode.AddNode($"Validity Not After: {header.ValidityNotAfter:yyyy-MM-dd HH:mm:ss}");
        headerNode.AddNode($"Creator: {System.Text.Encoding.UTF8.GetString(header.GetCreator()).TrimEnd('\0')}");
        var fascnBytes = header.GetFascN();
        var fascnHex = Convert.ToHexString(fascnBytes);
        var fascnNode = headerNode.AddNode($"FASC-N: {fascnHex}");
        DisplayFascNTree(fascnNode, fascnBytes);
        headerNode.AddNode($"Biometric Type: {header.BiometricType}");
        
        // Store header FASC-N for comparison with signature
        _headerFascN = fascnBytes;
        headerNode.AddNode($"Biometric Subtype: {header.BiometricSubtype}");
        headerNode.AddNode($"Quality: {header.Quality} ({GetQualityDescription(header.Quality)})");
        headerNode.AddNode($"BDB Length: {header.BdbLength:N0} bytes");
        headerNode.AddNode($"SB Length: {header.SbLength:N0} bytes");

        // Biometric data block information
        var bdbNode = tree.AddNode($"[green]Biometric Data Block[/] ({payload.BiometricDataBlock.Length:N0} bytes)");

        // If we have parsed biometric record, show detailed information
        if (payload.ParsedBiometricRecord.HasValue)
        {
            var record = payload.ParsedBiometricRecord.Value;
            var recordNode = bdbNode.AddNode($"[cyan]Parsed Biometric Record[/] ({record.GetType().Name})");
            
            // Use reflection to display properties
            var properties = record.GetType().GetProperties();
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(record);
                    if (value != null)
                    {
                        recordNode.AddNode($"{prop.Name}: {value}");
                    }
                }
                catch
                {
                    // Skip properties that can't be read
                }
            }
        }

        // Show hex dump if requested
        if (settings.ShowHex)
        {
            var hexNode = bdbNode.AddNode("[dim]Raw Data (first 256 bytes):[/]");
            var hexDump = GetHexDump(payload.BiometricDataBlock.Take(256).ToArray(), 16);
            hexNode.AddNode($"[dim]{hexDump}[/]");
        }

        // Signature information
        if (payload.Signature.HasValue)
        {
            AnalyzeSignatureBlock(tree.AddNode(""), payload.Signature.Value, settings);
        }
        else
        {
            tree.AddNode("[red]No Digital Signature[/]");
        }

        _console.Write(tree);
    }

    private static string GetFormatOwnerName(ushort owner) => owner switch
    {
        0x001B => "INCITS M1 (Biometrics)",
        0x0021 => "NIST",
        0x0001 => "Identix",
        0x0002 => "Digital Persona",
        _ => "Unknown"
    };

    private static string GetFormatTypeName(ushort type) => type switch
    {
        0x0101 => "Facial Image",
        0x0201 => "Fingerprint Minutiae",
        0x0301 => "Iris Image",
        _ => "Unknown"
    };

    private static string GetPatronFormatName(byte format) => format switch
    {
        0x03 => "PIV",
        0x04 => "PIV-I",
        0x05 => "CAC",
        _ => "Unknown"
    };

    private string GetQualityDescription(sbyte quality) => quality switch
    {
        -2 => "Permanently unusable (254 as byte) [yellow]⚠ INCITS 385-2004 requires 0[/]",
        -1 => "Temporarily unusable (255 as byte) [yellow]⚠ INCITS 385-2004 requires 0[/]",
        0 => "Unspecified (compliant with INCITS 385-2004)",
        >= 1 and <= 100 => $"{quality}% [yellow]⚠ INCITS 385-2004 requires 0 (SP 800-76-2 permits 0-100)[/]",
        _ => "Invalid"
    };

    private static string GetHexDump(byte[] data, int bytesPerLine = 16)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i += bytesPerLine)
        {
            var hex = string.Join(" ", data.Skip(i).Take(bytesPerLine).Select(b => $"{b:X2}"));
            var ascii = string.Join("", data.Skip(i).Take(bytesPerLine).Select(b => b >= 32 && b <= 126 ? (char)b : '.'));
            sb.AppendLine($"{i:X8}: {hex,-47} |{ascii}|");
        }
        return sb.ToString();
    }

    private async Task ExtractBiometricData(CbeffPayloadExtended payload, Settings settings)
    {
        try
        {
            var outputPath = settings.ExtractPath!;
            await File.WriteAllBytesAsync(outputPath, payload.BiometricDataBlock.ToArray());
            _console.MarkupLine($"[green]Extracted biometric data to:[/] {outputPath}");
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to extract data:[/] {ex.Message}");
        }
    }

    private void AnalyzeSignatureBlock(TreeNode parentTree, CbeffSignature signature, Settings settings)
    {
        var sigNode = parentTree.AddNode($"[green]Signature Block[/] ({signature.SignatureSize:N0} bytes)");
        sigNode.AddNode($"Type: {signature.SignatureType}");
        
        try
        {
            // Parse the CMS SignedData structure
            var cmsSignedData = new CmsSignedData(signature.SignatureData.ToArray());
            AnalyzeCmsSignedData(sigNode, cmsSignedData, settings);
        }
        catch (Exception ex)
        {
            sigNode.AddNode($"[red]CMS Parse Error:[/] {ex.Message.EscapeMarkup()}");
        }
    }

    private void AnalyzeCmsSignedData(TreeNode sigNode, CmsSignedData cmsSignedData, Settings settings)
    {
        // Content info
        var contentInfo = cmsSignedData.SignedContent;
        if (contentInfo != null)
        {
            var contentNode = sigNode.AddNode("[cyan]Content Info[/]");
            contentNode.AddNode($"Content Type: {cmsSignedData.SignedContentType}");
        }

        // Digest algorithms - using signed data properties directly
        var signedData = cmsSignedData.SignedData;
        if (signedData.DigestAlgorithms.Count > 0)
        {
            var digestNode = sigNode.AddNode($"[cyan]Digest Algorithms[/] ({signedData.DigestAlgorithms.Count})");
            foreach (var algo in signedData.DigestAlgorithms)
            {
                try
                {
                    var algorithmId = Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier.GetInstance(algo);
                    var algoName = GetDigestAlgorithmName(algorithmId.Algorithm.Id);
                    digestNode.AddNode($"{algoName} ({algorithmId.Algorithm.Id})");
                }
                catch (Exception ex)
                {
                    digestNode.AddNode($"[red]Parse Error:[/] {ex.Message.EscapeMarkup()}");
                }
            }
        }

        // Certificates
        var certificates = cmsSignedData.GetCertificates();
        var certList = new List<Org.BouncyCastle.X509.X509Certificate>(certificates.EnumerateMatches(null));
        if (certList.Count > 0)
        {
            var certsNode = sigNode.AddNode($"[cyan]Certificates[/] ({certList.Count})");
            for (int i = 0; i < certList.Count; i++)
            {
                AnalyzeCertificate(certsNode, certList[i], i, settings);
            }
        }

        // Signers
        var signers = cmsSignedData.GetSignerInfos();
        var signerList = new List<SignerInformation>(signers.GetSigners());
        if (signerList.Count > 0)
        {
            var signersNode = sigNode.AddNode($"[cyan]Signers[/] ({signerList.Count})");
            for (int i = 0; i < signerList.Count; i++)
            {
                AnalyzeSignerInformation(signersNode, signerList[i], i, settings);
            }
        }
        
        // Show hex dump if requested
        if (settings.ShowHex)
        {
            var rawData = cmsSignedData.GetEncoded();
            var hexDump = GetHexDump(rawData.Take(256).ToArray(), 16);
            sigNode.AddNode($"[dim]Encoded Data (first 256 bytes):[/]");
            sigNode.AddNode($"[dim]{hexDump}[/]");
        }
    }

    private void AnalyzeCertificate(TreeNode certsNode, Org.BouncyCastle.X509.X509Certificate cert, int index, Settings settings)
    {
        var certNode = certsNode.AddNode($"[cyan]Certificate {index}[/]");
        
        // Basic certificate info
        certNode.AddNode($"Subject: {cert.SubjectDN}");
        certNode.AddNode($"Issuer: {cert.IssuerDN}");
        certNode.AddNode($"Serial: {cert.SerialNumber}");
        certNode.AddNode($"Valid From: {cert.NotBefore:yyyy-MM-dd HH:mm:ss} UTC");
        certNode.AddNode($"Valid To: {cert.NotAfter:yyyy-MM-dd HH:mm:ss} UTC");
        
        // Check if certificate is currently valid
        var now = DateTime.UtcNow;
        var isValid = now >= cert.NotBefore && now <= cert.NotAfter;
        certNode.AddNode($"Currently Valid: {(isValid ? "[green]Yes[/]" : "[red]No[/]")}");
        
        // Algorithm info
        certNode.AddNode($"Signature Algorithm: {GetSignatureAlgorithmName(cert.SigAlgOid)} ({cert.SigAlgOid})");
        
        // Public key info
        var publicKey = cert.GetPublicKey();
        if (publicKey is Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters rsaKey)
        {
            certNode.AddNode($"Public Key Algorithm: RSA");
            certNode.AddNode($"Key Size: {rsaKey.Modulus.BitLength} bits");
        }
        else if (publicKey is Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters ecKey)
        {
            certNode.AddNode($"Public Key Algorithm: Elliptic Curve");
            certNode.AddNode($"Curve: {ecKey.Parameters.Curve}");
        }
        else
        {
            certNode.AddNode($"Public Key Algorithm: {publicKey.GetType().Name}");
        }
        
        // Show certificate hex if requested
        if (settings.ShowHex)
        {
            var certBytes = cert.GetEncoded();
            var hexDump = GetHexDump(certBytes.Take(256).ToArray(), 16);
            certNode.AddNode($"[dim]Certificate Data (first 256 bytes):[/]");
            certNode.AddNode($"[dim]{hexDump}[/]");
        }
    }

    private void AnalyzeSignerInformation(TreeNode signersNode, SignerInformation signer, int index, Settings settings)
    {
        var signerNode = signersNode.AddNode($"[yellow]Signer {index}[/]");
        
        // Signer identity
        var signerId = signer.SignerID;
        signerNode.AddNode($"Issuer: {signerId.Issuer}");
        signerNode.AddNode($"Serial Number: {signerId.SerialNumber}");
        if (signerId.SubjectKeyIdentifier != null)
        {
            signerNode.AddNode($"Subject Key ID: {Convert.ToHexString(signerId.SubjectKeyIdentifier)}");
        }
        
        // Algorithms
        var digestAlgoOid = signer.DigestAlgorithmID.Algorithm.Id;
        signerNode.AddNode($"Digest Algorithm: {GetDigestAlgorithmName(digestAlgoOid)} ({digestAlgoOid})");
        signerNode.AddNode($"Signature Algorithm: {GetEncryptionAlgorithmName(signer.SignatureAlgorithm.Algorithm.Id)} ({signer.SignatureAlgorithm.Algorithm.Id})");
        
        // Verify message digest if we have raw CBEFF data
        if (_rawCbeffData != null)
        {
            VerifyMessageDigest(signerNode, signer, digestAlgoOid);
        }
        
        // Signed attributes
        var signedAttrs = signer.SignedAttributes;
        if (signedAttrs != null && signedAttrs.Count > 0)
        {
            var signedAttrsNode = signerNode.AddNode($"[green]Signed Attributes[/] ({signedAttrs.Count})");
            foreach (Org.BouncyCastle.Asn1.Cms.Attribute attr in signedAttrs.ToAsn1EncodableVector())
            {
                AnalyzeAttribute(signedAttrsNode, attr);
            }
        }
        
        // Unsigned attributes
        var unsignedAttrs = signer.UnsignedAttributes;
        if (unsignedAttrs != null && unsignedAttrs.Count > 0)
        {
            var unsignedAttrsNode = signerNode.AddNode($"[cyan]Unsigned Attributes[/] ({unsignedAttrs.Count})");
            foreach (Org.BouncyCastle.Asn1.Cms.Attribute attr in unsignedAttrs.ToAsn1EncodableVector())
            {
                AnalyzeAttribute(unsignedAttrsNode, attr);
            }
        }
    }

    private void AnalyzeAttribute(TreeNode attrsNode, Org.BouncyCastle.Asn1.Cms.Attribute attr)
    {
        var oid = attr.AttrType.Id;
        var name = GetAttributeName(oid);
        var attrNode = attrsNode.AddNode($"{name} ({oid})");
        
        foreach (var value in attr.AttrValues)
        {
            try
            {
                // Special handling for FASC-N to add tree structure
                if (oid == "2.16.840.1.101.3.6.6")
                {
                    ParseAndDisplayPivFascN(attrNode, value.ToAsn1Object());
                }
                else
                {
                    var attrValue = GetAttributeValue(value.ToAsn1Object(), oid);
                    attrNode.AddNode($"Value: {attrValue}");
                }
            }
            catch (Exception ex)
            {
                attrNode.AddNode($"[red]Parse Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }
    }

    private async Task PerformCrossValidation(CbeffPayloadExtended payload, Settings settings)
    {
        _console.WriteLine();
        _console.MarkupLine("[yellow]PIV Cross-Validation[/]");
        
        if (!payload.Signature.HasValue)
        {
            _console.MarkupLine("[red]No signature present for cross-validation[/]");
            return;
        }

        try
        {
            var crossValidator = _serviceProvider.GetRequiredService<IPivCrossValidationService>();
            var trustedStore = _serviceProvider.GetRequiredService<ITrustedCertificateStoreExtended>();
            
            // Auto-detect CHUID if not provided
            var chuidFilePath = settings.ChuidFilePath;
            if (string.IsNullOrEmpty(chuidFilePath))
            {
                var autoDetectedChuid = crossValidator.AutoDetectChuidFile(settings.FilePath);
                if (autoDetectedChuid.HasValue)
                {
                    chuidFilePath = autoDetectedChuid.Value;
                    _console.MarkupLine($"[green]Auto-detected CHUID file:[/] {chuidFilePath}");
                }
            }

            // Convert CbeffPayloadExtended to CbeffPayload for cross-validation
            var basicPayloadResult = payload.ToBasicPayload();
            if (basicPayloadResult.IsFailure)
            {
                _console.MarkupLine($"[red]Failed to convert payload:[/] {basicPayloadResult.Error}");
                return;
            }
            var basicPayload = basicPayloadResult.Value;

            // Perform cross-validation
            var chuidMaybe = string.IsNullOrEmpty(chuidFilePath) 
                ? Maybe<string>.None 
                : Maybe<string>.From(chuidFilePath);
                
            var validationResult = await crossValidator.ValidateAsync(basicPayload, chuidMaybe, trustedStore);
            
            if (validationResult.IsSuccess)
            {
                DisplayCrossValidationResults(validationResult.Value);
            }
            else
            {
                _console.MarkupLine($"[red]Cross-validation failed:[/] {validationResult.Error}");
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Cross-validation error:[/] {ex.Message}");
        }
    }

    private void DisplayCrossValidationResults(PivCrossValidationResult result)
    {
        var tree = new Tree(result.IsValid ? "[green]✓ PIV Cross-Validation PASSED[/]" : "[red]✗ PIV Cross-Validation FAILED[/]");

        // CBEFF Validation Results
        var cbeffNode = tree.AddNode("[cyan]CBEFF Signature Validation[/]");
        cbeffNode.AddNode(result.CbeffValidation.IsValid ? "[green]✓ Valid[/]" : "[red]✗ Invalid[/]");
        
        if (result.CbeffValidation.SignerCertificate.HasValue)
        {
            cbeffNode.AddNode($"Signer: {result.CbeffValidation.SignerCertificate.Value.SubjectDN}");
        }
        
        if (result.CbeffValidation.ValidationErrors.Length > 0)
        {
            var errorsNode = cbeffNode.AddNode($"[red]Errors ({result.CbeffValidation.ValidationErrors.Length})[/]");
            foreach (var error in result.CbeffValidation.ValidationErrors)
            {
                errorsNode.AddNode($"[red]• {error}[/]");
            }
        }

        // CHUID Validation Results
        if (result.ChuidValidation.HasValue)
        {
            var chuidNode = tree.AddNode("[cyan]CHUID Signature Validation[/]");
            var chuid = result.ChuidValidation.Value;
            
            chuidNode.AddNode(chuid.SignatureValid ? "[green]✓ Valid[/]" : "[red]✗ Invalid[/]");
            
            if (chuid.SignerCertificate.HasValue)
            {
                chuidNode.AddNode($"Signer: {chuid.SignerCertificate.Value.SubjectDN}");
            }
            
            if (chuid.ValidationErrors.Length > 0)
            {
                var errorsNode = chuidNode.AddNode($"[red]Errors ({chuid.ValidationErrors.Length})[/]");
                foreach (var error in chuid.ValidationErrors)
                {
                    errorsNode.AddNode($"[red]• {error}[/]");
                }
            }

            // Display CHUID details
            var chuidStructNode = chuidNode.AddNode("[dim]CHUID Structure[/]");
            if (chuid.ChuidStructure.FascN != null)
            {
                var fascnHex = Convert.ToHexString(chuid.ChuidStructure.FascN);
                chuidStructNode.AddNode($"FASC-N: {fascnHex}");
            }
            if (chuid.ChuidStructure.CardUuid.HasValue)
            {
                chuidStructNode.AddNode($"Card UUID: {chuid.ChuidStructure.CardUuid.Value}");
            }
            if (chuid.ChuidStructure.ExpirationDate.HasValue)
            {
                chuidStructNode.AddNode($"Expiration: {chuid.ChuidStructure.ExpirationDate.Value:yyyy-MM-dd}");
            }
        }
        else
        {
            tree.AddNode("[yellow]⚠ No CHUID available for cross-validation[/]");
        }

        // Certificate Discovery Results
        var discoveryNode = tree.AddNode("[cyan]Certificate Discovery[/]");
        discoveryNode.AddNode($"Validation Strategy: {result.ValidationStrategy}");
        discoveryNode.AddNode($"Signer Identity Match: {(result.CertificateDiscovery.SignerIdentityMatch ? "[green]✓ Yes[/]" : "[yellow]⚠ No[/]")}");
        discoveryNode.AddNode($"Recommended Certificate: {result.CertificateDiscovery.RecommendedCertificate.SubjectDN}");
        
        if (result.CertificateDiscovery.CbeffSignerCertificate.HasValue)
        {
            discoveryNode.AddNode($"CBEFF Certificate: {result.CertificateDiscovery.CbeffSignerCertificate.Value.SubjectDN}");
        }
        
        if (result.CertificateDiscovery.ChuidSignerCertificate.HasValue)
        {
            discoveryNode.AddNode($"CHUID Certificate: {result.CertificateDiscovery.ChuidSignerCertificate.Value.SubjectDN}");
        }

        // Certificate Chain Validation
        var chainNode = tree.AddNode("[cyan]Certificate Chain Validation[/]");
        chainNode.AddNode(result.ChainValidation.IsValid ? "[green]✓ Valid[/]" : "[red]✗ Invalid[/]");
        
        if (result.ChainValidation.TrustedRoot.HasValue)
        {
            chainNode.AddNode($"Trusted Root: {result.ChainValidation.TrustedRoot.Value.SubjectDN}");
        }
        
        chainNode.AddNode($"Chain Length: {result.ChainValidation.ChainPath.Length} certificates");
        
        if (result.ChainValidation.ValidationErrors.Length > 0)
        {
            var chainErrorsNode = chainNode.AddNode($"[red]Chain Errors ({result.ChainValidation.ValidationErrors.Length})[/]");
            foreach (var error in result.ChainValidation.ValidationErrors)
            {
                chainErrorsNode.AddNode($"[red]• {error}[/]");
            }
        }

        // Cross-Validation Checks
        var crossChecksNode = tree.AddNode("[cyan]Cross-Validation Checks[/]");
        var checks = result.CrossValidationChecks;
        
        crossChecksNode.AddNode($"FASC-N Consistency: {(checks.FascNMatches ? "[green]✓ Matches[/]" : "[red]✗ Mismatch[/]")}");
        crossChecksNode.AddNode($"Signer Identity: {(checks.SignerIdentityMatches ? "[green]✓ Correct PIV Behavior[/]" : "[red]✗ Invalid Signer Identity[/]")}");
        crossChecksNode.AddNode($"Card UUID Consistency: {(checks.CardUuidMatches ? "[green]✓ Matches[/]" : "[yellow]⚠ Mismatch or N/A[/]")}");
        crossChecksNode.AddNode($"Timestamp Consistency: {(checks.ValidationTimestampConsistency ? "[green]✓ Consistent[/]" : "[yellow]⚠ Inconsistent or N/A[/]")}");

        // Overall validation errors
        if (result.ValidationErrors.Length > 0)
        {
            var allErrorsNode = tree.AddNode($"[red]All Validation Errors ({result.ValidationErrors.Length})[/]");
            foreach (var error in result.ValidationErrors)
            {
                allErrorsNode.AddNode($"[red]• {error}[/]");
            }
        }

        _console.Write(tree);
    }

    private async Task VerifySignature(CbeffPayloadExtended payload, Settings settings)
    {
        _console.WriteLine();
        _console.MarkupLine("[yellow]Signature Verification[/]");
        
        if (!payload.Signature.HasValue)
        {
            _console.MarkupLine("[red]No signature present[/]");
            return;
        }

        try
        {
            var verifier = _serviceProvider.GetRequiredService<ICbeffVerifier>();
            var certificateStore = _serviceProvider.GetRequiredService<ITrustedCertificateStore>();
            
            // Get trusted certificates from the store
            IStore<X509Certificate> trustedCerts;
            
            // If a certificate path was provided, load it
            if (!string.IsNullOrEmpty(settings.CertificatePath))
            {
                if (File.Exists(settings.CertificatePath))
                {
                    var certBytes = File.ReadAllBytes(settings.CertificatePath);
                    var cert = new X509Certificate(certBytes);
                    var certList = new List<X509Certificate> { cert };
                    trustedCerts = CollectionUtilities.CreateStore(certList);
                    _console.MarkupLine($"[green]Loaded certificate:[/] {cert.SubjectDN}");
                }
                else
                {
                    _console.MarkupLine($"[red]Certificate file not found:[/] {settings.CertificatePath}");
                    return;
                }
            }
            else
            {
                // Use certificates from the trusted store
                var storeResult = certificateStore.GetTrustedCertificates();
                if (storeResult.IsFailure)
                {
                    _console.MarkupLine($"[red]Failed to get trusted certificates:[/] {storeResult.Error}");
                    return;
                }
                
                trustedCerts = storeResult.Value;
            }

            // Convert CbeffPayloadExtended to CbeffPayload for verification
            var basicPayloadResult = payload.ToBasicPayload();
            if (basicPayloadResult.IsFailure)
            {
                _console.MarkupLine($"[red]Failed to convert payload:[/] {basicPayloadResult.Error}");
                return;
            }
            var basicPayload = basicPayloadResult.Value;

            var verificationResult = await verifier.VerifyAsync(basicPayload, trustedCerts);
            
            if (verificationResult.IsSuccess)
            {
                var result = verificationResult.Value;
                
                if (result.IsValid)
                {
                    _console.MarkupLine("[green]✓ Signature verification PASSED[/]");
                    
                    if (result.SignerCertificate.HasValue)
                    {
                        _console.MarkupLine($"[green]Signer:[/] {result.SignerCertificate.Value.SubjectDN}");
                    }
                    
                    if (result.SigningTime.HasValue)
                    {
                        _console.MarkupLine($"[green]Signing Time:[/] {result.SigningTime.Value:yyyy-MM-dd HH:mm:ss} UTC");
                    }
                    
                    if (result.CardUuid.HasValue)
                    {
                        _console.MarkupLine($"[green]Card UUID:[/] {result.CardUuid.Value}");
                    }
                }
                else
                {
                    _console.MarkupLine("[red]✗ Signature verification FAILED[/]");
                    
                    foreach (var error in result.ValidationErrors)
                    {
                        _console.MarkupLine($"[red]Error:[/] {error}");
                    }
                }
            }
            else
            {
                _console.MarkupLine($"[red]Verification failed:[/] {verificationResult.Error}");
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Verification error:[/] {ex.Message}");
        }
    }

    private string GetDigestAlgorithmName(string oid) => oid switch
    {
        "2.16.840.1.101.3.4.2.1" => "SHA-256",
        "2.16.840.1.101.3.4.2.2" => "SHA-384",
        "2.16.840.1.101.3.4.2.3" => "SHA-512",
        "1.3.14.3.2.26" => "SHA-1",
        "1.2.840.113549.2.5" => "MD5",
        _ => $"Unknown ({oid})"
    };

    private string GetEncryptionAlgorithmName(string oid) => oid switch
    {
        "1.2.840.113549.1.1.1" => "RSA",
        "1.2.840.113549.1.1.11" => "RSA with SHA-256",
        "1.2.840.113549.1.1.12" => "RSA with SHA-384",
        "1.2.840.113549.1.1.13" => "RSA with SHA-512",
        "1.2.840.10045.2.1" => "ECDSA",
        "1.2.840.10045.4.3.2" => "ECDSA with SHA-256",
        "1.2.840.10045.4.3.3" => "ECDSA with SHA-384",
        "1.2.840.10045.4.3.4" => "ECDSA with SHA-512",
        _ => $"Unknown ({oid})"
    };

    private string GetAttributeName(string oid) => oid switch
    {
        // PKCS#9 attributes
        "1.2.840.113549.1.9.3" => "Content Type",
        "1.2.840.113549.1.9.4" => "Message Digest",
        "1.2.840.113549.1.9.5" => "Signing Time",
        "1.2.840.113549.1.9.6" => "Counter Signature",
        
        // PIV-specific attributes (NIST SP 800-73-4)
        "2.16.840.1.101.3.6.5" => "pivSigner-DN",
        "2.16.840.1.101.3.6.6" => "pivFASC-N",
        
        // UUID/GUID attributes
        "1.3.6.1.1.16.4" => "UUID",
        "1.2.840.113549.1.9.16.2.12" => "Signing Certificate",
        "1.2.840.113549.1.9.16.2.47" => "Signing Certificate V2",
        "2.16.840.1.101.3.6.9.1" => "Card UUID",
        _ => $"Unknown ({oid})"
    };

    private string GetAttributeValue(Org.BouncyCastle.Asn1.Asn1Object attributeValue, string oid = "")
    {
        try
        {
            // Handle PIV-specific OIDs with special parsing
            return oid switch
            {
                "2.16.840.1.101.3.6.5" => ParsePivSignerDn(attributeValue), // pivSigner-DN
                "2.16.840.1.101.3.6.6" => ParsePivFascN(attributeValue),    // pivFASC-N
                "1.3.6.1.1.16.4" => ParseUuid(attributeValue),              // UUID
                _ => ParseStandardAttributeValue(attributeValue)
            };
        }
        catch (Exception ex)
        {
            return $"[Parse Error: {ex.Message.EscapeMarkup()}]"; 
        }
    }

    private string ParseStandardAttributeValue(Org.BouncyCastle.Asn1.Asn1Object attributeValue)
    {
        return attributeValue switch
        {
            Org.BouncyCastle.Asn1.DerOctetString octetString => Convert.ToHexString(octetString.GetOctets()),
            Org.BouncyCastle.Asn1.DerUtf8String utf8String => utf8String.GetString(),
            Org.BouncyCastle.Asn1.DerPrintableString printableString => printableString.GetString(),
            Org.BouncyCastle.Asn1.DerIA5String ia5String => ia5String.GetString(),
            Org.BouncyCastle.Asn1.DerInteger integer => integer.Value.ToString(),
            Org.BouncyCastle.Asn1.DerUtcTime utcTime => utcTime.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss UTC"),
            Org.BouncyCastle.Asn1.DerGeneralizedTime genTime => genTime.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss UTC"),
            Org.BouncyCastle.Asn1.Asn1UtcTime asn1UtcTime => asn1UtcTime.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss UTC"),
            Org.BouncyCastle.Asn1.DerObjectIdentifier oid => $"OID: {oid.Id} ({GetOidDescription(oid.Id)})",
            _ => $"[[{attributeValue.GetType().Name.EscapeMarkup()}]] {Convert.ToHexString(attributeValue.GetEncoded())}"
        };
    }

    private string ParsePivSignerDn(Org.BouncyCastle.Asn1.Asn1Object attributeValue)
    {
        try
        {
            // The pivSigner-DN should be an X.501 Name (constructed ASN.1)
            if (attributeValue is Org.BouncyCastle.Asn1.Asn1Sequence sequence)
            {
                var x500Name = Org.BouncyCastle.Asn1.X509.X509Name.GetInstance(sequence);
                return x500Name.ToString();
            }
            else if (attributeValue is Org.BouncyCastle.Asn1.DerOctetString octetString)
            {
                // Sometimes the DN is wrapped in an OCTET STRING
                var innerBytes = octetString.GetOctets();
                var innerObject = Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(innerBytes);
                if (innerObject is Org.BouncyCastle.Asn1.Asn1Sequence innerSequence)
                {
                    var x500Name = Org.BouncyCastle.Asn1.X509.X509Name.GetInstance(innerSequence);
                    return x500Name.ToString();
                }
            }
            
            // Try parsing the raw bytes directly
            var rawBytes = attributeValue.GetEncoded();
            var parsedObject = Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(rawBytes);
            if (parsedObject is Org.BouncyCastle.Asn1.Asn1Sequence directSequence)
            {
                var x500Name = Org.BouncyCastle.Asn1.X509.X509Name.GetInstance(directSequence);
                return x500Name.ToString();
            }
            
            // Fallback to hex if we can't parse as X.501 Name
            return $"[X.501 Name - could not parse] {Convert.ToHexString(attributeValue.GetEncoded())}";
        }
        catch (Exception ex)
        {
            return $"[X.501 Name parse error: {ex.Message.EscapeMarkup()}] {Convert.ToHexString(attributeValue.GetEncoded())}";
        }
    }

    private string ParsePivFascN(Org.BouncyCastle.Asn1.Asn1Object attributeValue)
    {
        // Note: For tree display of FASC-N in attributes, use ParseAndDisplayPivFascN instead
        try
        {
            if (attributeValue is Org.BouncyCastle.Asn1.DerOctetString octetString)
            {
                var fascnBytes = octetString.GetOctets();
                return Convert.ToHexString(fascnBytes);
            }
            return $"[FASC-N] {Convert.ToHexString(attributeValue.GetEncoded())}";
        }
        catch
        {
            return $"[FASC-N parse error] {Convert.ToHexString(attributeValue.GetEncoded())}";
        }
    }

    private string ParseUuid(Org.BouncyCastle.Asn1.Asn1Object attributeValue)
    {
        try
        {
            if (attributeValue is Org.BouncyCastle.Asn1.DerOctetString octetString)
            {
                var uuidBytes = octetString.GetOctets();
                if (uuidBytes.Length == 16)
                {
                    var guid = new Guid(uuidBytes);
                    return $"UUID: {guid}";
                }
            }
            return $"[UUID] {Convert.ToHexString(attributeValue.GetEncoded())}";
        }
        catch
        {
            return $"[UUID parse error] {Convert.ToHexString(attributeValue.GetEncoded())}";
        }
    }

    private string GetSignatureAlgorithmName(string oid) => oid switch
    {
        "1.2.840.113549.1.1.1" => "RSA",
        "1.2.840.113549.1.1.5" => "RSA with SHA-1",
        "1.2.840.113549.1.1.11" => "RSA with SHA-256",
        "1.2.840.113549.1.1.12" => "RSA with SHA-384",
        "1.2.840.113549.1.1.13" => "RSA with SHA-512",
        "1.2.840.10045.4.1" => "ECDSA with SHA-1",
        "1.2.840.10045.4.3.2" => "ECDSA with SHA-256",
        "1.2.840.10045.4.3.3" => "ECDSA with SHA-384",
        "1.2.840.10045.4.3.4" => "ECDSA with SHA-512",
        _ => $"Unknown ({oid})"
    };

    private string GetOidDescription(string oid) => oid switch
    {
        // Common content types
        "1.2.840.113549.1.7.1" => "PKCS #7 Data",
        "1.2.840.113549.1.7.2" => "PKCS #7 Signed Data",
        "1.2.840.113549.1.7.3" => "PKCS #7 Enveloped Data",
        "1.2.840.113549.1.7.4" => "PKCS #7 Signed and Enveloped Data",
        "1.2.840.113549.1.7.5" => "PKCS #7 Digested Data",
        "1.2.840.113549.1.7.6" => "PKCS #7 Encrypted Data",
        
        // PIV content types
        "2.16.840.1.101.3.6.2" => "PIV Biometric Object",
        "2.16.840.1.101.3.6.3" => "PIV Security Object",
        "2.16.840.1.101.3.6.4" => "PIV Content Signing",
        
        // NIST algorithms
        "2.16.840.1.101.3.4.2.1" => "SHA-256",
        "2.16.840.1.101.3.4.2.2" => "SHA-384", 
        "2.16.840.1.101.3.4.2.3" => "SHA-512",
        
        // Encryption algorithms
        "1.2.840.113549.1.1.1" => "RSA Encryption",
        "1.2.840.113549.1.1.11" => "RSA with SHA-256",
        "1.2.840.10045.2.1" => "Elliptic Curve Public Key",
        
        // Hash algorithms
        "1.3.14.3.2.26" => "SHA-1",
        "1.2.840.113549.2.5" => "MD5",
        
        _ => "Unknown OID"
    };

    private string DecodeFascN(byte[] fascnBytes)
    {
        if (fascnBytes == null || fascnBytes.Length == 0)
            return "Invalid FASC-N";

        var result = FascNCodec.Decode(fascnBytes);
        return result.IsSuccess ? result.Value.ToString() : $"Parse error: {result.Error}";
    }

    private void DisplayFascNTree(TreeNode fascnNode, byte[] fascnBytes)
    {
        if (fascnBytes == null || fascnBytes.Length == 0)
        {
            fascnNode.AddNode("[red]Invalid FASC-N data[/]");
            return;
        }

        var result = FascNCodec.Decode(fascnBytes);
        if (result.IsFailure)
        {
            fascnNode.AddNode($"[red]Parse error:[/] {result.Error}");
            return;
        }

        var fascn = result.Value;
        
        fascnNode.AddNode($"Credential: {fascn.CredentialNumber:D6} (Series: {fascn.CredentialSeries}, Issue: {fascn.IndividualCredentialIssue})");
        fascnNode.AddNode($"Agency/System: {fascn.AgencyCode:D4} / {fascn.SystemCode:D4}");
        fascnNode.AddNode($"Person ID: {fascn.PersonIdentifier:D10}");
        fascnNode.AddNode($"Organization: {fascn.OrganizationalIdentifier:D4} ({fascn.GetCategoryDescription()})");
        fascnNode.AddNode($"Association: {fascn.GetAffiliationDescription()}");
    }

    private void ParseAndDisplayPivFascN(TreeNode parentNode, Org.BouncyCastle.Asn1.Asn1Object attributeValue)
    {
        try
        {
            if (attributeValue is Org.BouncyCastle.Asn1.DerOctetString octetString)
            {
                var fascnBytes = octetString.GetOctets();
                var fascnHex = Convert.ToHexString(fascnBytes);
                
                // Compare with header FASC-N if available
                string comparisonResult = "";
                if (_headerFascN != null)
                {
                    if (fascnBytes.SequenceEqual(_headerFascN))
                    {
                        comparisonResult = " [green]✓ Matches CBEFF header[/]";
                    }
                    else
                    {
                        comparisonResult = " [red]✗ MISMATCH with CBEFF header[/]";
                        _console.MarkupLine("[yellow]WARNING:[/] FASC-N in signature does not match FASC-N in CBEFF header!");
                    }
                }
                
                var fascnNode = parentNode.AddNode($"Value: {fascnHex}{comparisonResult}");
                DisplayFascNTree(fascnNode, fascnBytes);
            }
            else
            {
                parentNode.AddNode($"[FASC-N] {Convert.ToHexString(attributeValue.GetEncoded())}");
            }
        }
        catch (Exception ex)
        {
            parentNode.AddNode($"[FASC-N parse error: {ex.Message.EscapeMarkup()}] {Convert.ToHexString(attributeValue.GetEncoded())}");
        }
    }

    private void VerifyMessageDigest(TreeNode signerNode, SignerInformation signer, string digestAlgoOid)
    {
        try
        {
            // Get the MessageDigest attribute from signed attributes
            var messageDigestAttr = signer.SignedAttributes?[CmsAttributes.MessageDigest];
            if (messageDigestAttr == null)
            {
                signerNode.AddNode("[red]✗ No MessageDigest attribute found[/]");
                return;
            }

            var messageDigestValue = messageDigestAttr.AttrValues.GetEnumerator();
            if (!messageDigestValue.MoveNext())
            {
                signerNode.AddNode("[red]✗ MessageDigest attribute has no value[/]");
                return;
            }

            var expectedDigest = Org.BouncyCastle.Asn1.Asn1OctetString.GetInstance(messageDigestValue.Current).GetOctets();
            
            // Calculate the digest over the signed portion (header + BDB)
            // The signature covers everything except the signature block itself
            var cbeffData = _rawCbeffData!;
            
            // Parse to find where signature block starts
            // CBEFF structure: PIV BC Tag (optional) + Header + BDB + Signature
            // We need to hash: Header + BDB
            
            // First check if we have PIV BC tag
            if (BerTlv.HasBerTag(cbeffData, 0xBC))
            {
                var extractResult = BerTlv.ExtractValueIfTagged(cbeffData, 0xBC);
                if (extractResult.IsSuccess)
                {
                    cbeffData = extractResult.Value;
                }
            }
            
            // CBEFF Header is fixed 88 bytes
            const int cbeffHeaderSize = 88;
            if (cbeffData.Length < cbeffHeaderSize)
            {
                signerNode.AddNode("[red]✗ CBEFF data too short for header[/]");
                return;
            }
            
            // Get BDB length from header (bytes 2-5, big-endian)
            int bdbLength = (cbeffData[2] << 24) | (cbeffData[3] << 16) | (cbeffData[4] << 8) | cbeffData[5];
            
            // Calculate signed portion length (header + BDB)
            int signedPortionLength = cbeffHeaderSize + bdbLength;
            if (cbeffData.Length < signedPortionLength)
            {
                signerNode.AddNode($"[red]✗ CBEFF data too short: expected {signedPortionLength}, got {cbeffData.Length}[/]");
                return;
            }
            
            // Extract the signed portion
            var signedData = new byte[signedPortionLength];
            Array.Copy(cbeffData, 0, signedData, 0, signedPortionLength);
            
            // Calculate digest based on algorithm
            byte[] calculatedDigest;
            switch (digestAlgoOid)
            {
                case "2.16.840.1.101.3.4.2.1": // SHA-256
                    var sha256 = new Org.BouncyCastle.Crypto.Digests.Sha256Digest();
                    sha256.BlockUpdate(signedData, 0, signedData.Length);
                    calculatedDigest = new byte[sha256.GetDigestSize()];
                    sha256.DoFinal(calculatedDigest, 0);
                    break;
                    
                case "2.16.840.1.101.3.4.2.2": // SHA-384
                    var sha384 = new Org.BouncyCastle.Crypto.Digests.Sha384Digest();
                    sha384.BlockUpdate(signedData, 0, signedData.Length);
                    calculatedDigest = new byte[sha384.GetDigestSize()];
                    sha384.DoFinal(calculatedDigest, 0);
                    break;
                    
                case "1.3.14.3.2.26": // SHA-1
                    var sha1 = new Org.BouncyCastle.Crypto.Digests.Sha1Digest();
                    sha1.BlockUpdate(signedData, 0, signedData.Length);
                    calculatedDigest = new byte[sha1.GetDigestSize()];
                    sha1.DoFinal(calculatedDigest, 0);
                    break;
                    
                default:
                    signerNode.AddNode($"[yellow]⚠ Unsupported digest algorithm: {digestAlgoOid}[/]");
                    return;
            }
            
            // Compare digests
            var digestNode = signerNode.AddNode("[cyan]Message Digest Verification[/]");
            digestNode.AddNode($"Algorithm: {GetDigestAlgorithmName(digestAlgoOid)}");
            digestNode.AddNode($"Expected: {Convert.ToHexString(expectedDigest)}");
            digestNode.AddNode($"Calculated: {Convert.ToHexString(calculatedDigest)}");
            
            if (expectedDigest.SequenceEqual(calculatedDigest))
            {
                digestNode.AddNode("[green]✓ Message digest is CORRECT[/]");
            }
            else
            {
                digestNode.AddNode("[red]✗ Message digest MISMATCH[/]");
                digestNode.AddNode($"[red]Signed data length: {signedData.Length} bytes (header: {cbeffHeaderSize}, BDB: {bdbLength})[/]");
            }
        }
        catch (Exception ex)
        {
            signerNode.AddNode($"[red]✗ Error verifying digest: {ex.Message}[/]");
        }
    }
}