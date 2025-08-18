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
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Asn1.Cms;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Utilities.Collections;

/// <summary>
/// Command to inspect CBEFF files and display their structure with detailed CMS analysis.
/// </summary>
public sealed class InspectCbeffCommand : AsyncCommand<InspectCbeffCommand.Settings>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICbeffSerializerExtended _serializer;
    private readonly IAnsiConsole _console;

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
            await VerifySignature(payload, settings);

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
        headerNode.AddNode($"FASC-N: {Convert.ToHexString(header.GetFascN())}");
        headerNode.AddNode($"Biometric Type: {header.BiometricType}");
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
            sigNode.AddNode($"[red]CMS Parse Error:[/] {ex.Message}");
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
                    digestNode.AddNode($"[red]Parse Error:[/] {ex.Message}");
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
        signerNode.AddNode($"Digest Algorithm: {GetDigestAlgorithmName(signer.DigestAlgorithmID.Algorithm.Id)} ({signer.DigestAlgorithmID.Algorithm.Id})");
        signerNode.AddNode($"Encryption Algorithm: {GetEncryptionAlgorithmName(signer.EncryptionAlgorithmID.Algorithm.Id)} ({signer.EncryptionAlgorithmID.Algorithm.Id})");
        
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
                var attrValue = GetAttributeValue(value.ToAsn1Object());
                attrNode.AddNode($"Value: {attrValue}");
            }
            catch (Exception ex)
            {
                attrNode.AddNode($"[red]Parse Error:[/] {ex.Message}");
            }
        }
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
                var certCount = trustedCerts.EnumerateMatches(null).Count();
                _console.MarkupLine($"[green]Using {certCount} certificates from trusted store[/]");
                
                // Show certificate subjects for debugging
                foreach (var cert in trustedCerts.EnumerateMatches(null))
                {
                    _console.MarkupLine($"[dim]  - {cert.SubjectDN}[/]");
                    _console.MarkupLine($"[dim]    Serial: {cert.SerialNumber}[/]");
                }
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
        "1.2.840.113549.1.9.3" => "Content Type",
        "1.2.840.113549.1.9.4" => "Message Digest",
        "1.2.840.113549.1.9.5" => "Signing Time",
        "1.2.840.113549.1.9.6" => "Counter Signature",
        "1.2.840.113549.1.9.16.2.12" => "Signing Certificate",
        "1.2.840.113549.1.9.16.2.47" => "Signing Certificate V2",
        "2.16.840.1.101.3.6.9.1" => "Card UUID",
        _ => $"Unknown ({oid})"
    };

    private string GetAttributeValue(Org.BouncyCastle.Asn1.Asn1Object attributeValue)
    {
        try
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
                _ => $"[{attributeValue.GetType().Name}] {Convert.ToHexString(attributeValue.GetEncoded())}"
            };
        }
        catch
        {
            return "[Parse Error]"; 
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
}