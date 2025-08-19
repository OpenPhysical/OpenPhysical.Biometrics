namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Domains.Cbeff;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.X509;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

/// <summary>
/// Service for discovering and validating certificates from multiple sources.
/// Implements PIV specification requirements for CBEFF-CHUID cross-validation.
/// </summary>
public sealed class CertificateDiscoveryService : ICertificateDiscoveryService
{
    private readonly ILogger<CertificateDiscoveryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateDiscoveryService"/> class.
    /// </summary>
    public CertificateDiscoveryService(ILogger<CertificateDiscoveryService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers all available certificates for a CBEFF signature verification.
    /// Implements PIV specification rules for certificate selection.
    /// </summary>
    public Result<CertificateDiscoveryResult> DiscoverCertificates(
        CbeffPayload cbeffPayload,
        Maybe<ChuidStructure> chuidStructure,
        ITrustedCertificateStoreExtended trustedStore)
    {
        try
        {
            _logger.LogDebug("Starting certificate discovery process");
            
            // Extract CBEFF signer certificate
            _logger.LogDebug("Extracting CBEFF signer certificate...");
            var cbeffCert = ExtractCbeffSignerCertificate(cbeffPayload);
            if (cbeffCert.HasValue)
            {
                _logger.LogDebug("Successfully extracted CBEFF signer certificate: {Subject}", cbeffCert.Value.SubjectDN);
            }
            else
            {
                _logger.LogWarning("No CBEFF signer certificate found");
            }
            
            // Extract CHUID signer certificate if available
            _logger.LogDebug("Extracting CHUID signer certificate...");
            var chuidCert = chuidStructure.HasValue 
                ? ExtractChuidSignerCertificate(chuidStructure.Value)
                : Maybe<X509Certificate>.None;
            
            if (chuidCert.HasValue)
            {
                _logger.LogDebug("Successfully extracted CHUID signer certificate: {Subject}", chuidCert.Value.SubjectDN);
            }
            else
            {
                _logger.LogDebug("No CHUID signer certificate found (CHUID available: {HasChuid})", chuidStructure.HasValue);
            }

            // Find matching certificate in trusted store - try CBEFF cert first, then CHUID cert
            _logger.LogDebug("Finding matching certificate in trusted store...");
            var trustedCert = Maybe<X509Certificate>.None;
            
            if (cbeffCert.HasValue)
            {
                trustedCert = FindInTrustedStore(cbeffCert.Value, trustedStore);
            }
            else if (chuidCert.HasValue)
            {
                // If no CBEFF cert, try to match CHUID cert in trusted store
                trustedCert = FindInTrustedStore(chuidCert.Value, trustedStore);
            }
                
            if (trustedCert.HasValue)
            {
                _logger.LogDebug("Found matching trusted certificate: {Subject}", trustedCert.Value.SubjectDN);
            }
            else
            {
                _logger.LogDebug("No matching trusted certificate found");
            }

            // Determine signer identity match
            _logger.LogDebug("Determining signer identity match...");
            var signerIdentityMatch = false;
            
            if (cbeffCert.HasValue && chuidCert.HasValue)
            {
                // Both certificates available - compare them
                signerIdentityMatch = AreSameSignerIdentity(cbeffCert.Value, chuidCert.Value);
                _logger.LogDebug("Signer identity comparison: {Match}", signerIdentityMatch);
            }
            else if (!cbeffCert.HasValue && chuidCert.HasValue)
            {
                // CBEFF has no embedded cert - using CHUID cert is correct PIV behavior
                signerIdentityMatch = true;
                _logger.LogDebug("Signer identity: Using CHUID certificate (CBEFF has no embedded cert - normal PIV behavior)");
            }
            else if (cbeffCert.HasValue && !chuidCert.HasValue)
            {
                // CBEFF cert only - acceptable
                signerIdentityMatch = true;  
                _logger.LogDebug("Signer identity: Using CBEFF certificate (no CHUID available)");
            }
            else
            {
                // No certificates available
                signerIdentityMatch = false;
                _logger.LogDebug("Signer identity: No certificates available");
            }
            
            _logger.LogDebug("Final signer identity match result: {Match} (CBEFF cert: {HasCbeff}, CHUID cert: {HasChuid})", 
                signerIdentityMatch, cbeffCert.HasValue, chuidCert.HasValue);

            // Apply PIV validation strategy
            _logger.LogDebug("Determining PIV validation strategy...");
            var (recommendedCert, strategy) = DetermineValidationStrategy(
                cbeffCert, chuidCert, trustedCert, signerIdentityMatch);
            _logger.LogDebug("Selected validation strategy: {Strategy}", strategy);

            if (recommendedCert.IsFailure)
            {
                _logger.LogError("Could not determine recommended certificate: {Error}", recommendedCert.Error);
                return Result.Failure<CertificateDiscoveryResult>(
                    $"Could not determine recommended certificate: {recommendedCert.Error}");
            }
            
            _logger.LogDebug("Successfully determined recommended certificate: {Subject}", 
                recommendedCert.Value.SubjectDN);

            return Result.Success(new CertificateDiscoveryResult(
                cbeffCert,
                chuidCert,
                trustedCert,
                recommendedCert.Value,
                signerIdentityMatch,
                strategy));
        }
        catch (Exception ex)
        {
            return Result.Failure<CertificateDiscoveryResult>($"Error during certificate discovery: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a certificate chain back to a trusted root.
    /// </summary>
    public Result<CertificateChainValidationResult> ValidateCertificateChain(
        X509Certificate certificate,
        ITrustedCertificateStoreExtended trustedStore)
    {
        var errors = new List<string>();
        
        try
        {
            // Build certificate chain
            var chainResult = BuildCertificateChain(certificate, trustedStore);
            if (chainResult.IsFailure)
            {
                errors.Add(chainResult.Error);
                return Result.Success(new CertificateChainValidationResult(
                    false, Maybe<X509Certificate>.None, ImmutableArray<X509Certificate>.Empty, 
                    errors.ToImmutableArray()));
            }

            var chain = chainResult.Value;
            
            // Validate each certificate in the chain
            for (int i = 0; i < chain.Length - 1; i++)
            {
                var cert = chain[i];
                var issuer = chain[i + 1];
                
                _logger.LogDebug("Validating cert {Subject} with issuer {Issuer}", cert.SubjectDN, issuer.SubjectDN);
                
                // Check if issuer has keyCertSign key usage (required to sign certificates)
                // Note: We check all issuers, not just intermediate CAs
                if (!HasKeyCertSignUsage(issuer))
                {
                    _logger.LogWarning("Issuer certificate lacks keyCertSign key usage: {Issuer}", issuer.SubjectDN);
                    errors.Add($"Issuer certificate lacks keyCertSign key usage: {issuer.SubjectDN}");
                    // Still try to verify - the error will be more specific
                }
                
                // Verify signature
                try
                {
                    _logger.LogDebug("Verifying signature on {CertSubject} (Serial: {CertSerial}) with issuer {IssuerSubject} (Serial: {IssuerSerial})",
                        cert.SubjectDN, cert.SerialNumber, issuer.SubjectDN, issuer.SerialNumber);
                    
                    // Check basic issuer/subject relationship
                    if (!cert.IssuerDN.Equals(issuer.SubjectDN))
                    {
                        _logger.LogWarning("Certificate issuer DN mismatch: cert issuer={CertIssuer}, actual issuer={ActualIssuer}",
                            cert.IssuerDN, issuer.SubjectDN);
                    }
                    
                    cert.Verify(issuer.GetPublicKey());
                    _logger.LogDebug("Certificate signature verification passed for {Subject}", cert.SubjectDN);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Certificate verification failed for {Subject}", cert.SubjectDN);
                    errors.Add($"Certificate signature verification failed for {cert.SubjectDN}: {ex.Message}");
                }
                
                // Check validity period
                var now = System.DateTime.UtcNow;
                if (now < cert.NotBefore || now > cert.NotAfter)
                {
                    errors.Add($"Certificate not valid at current time: {cert.SubjectDN}");
                }
            }

            // Find trusted root
            var trustedRoot = chain.Length > 0 ? Maybe<X509Certificate>.From(chain[^1]) : Maybe<X509Certificate>.None;
            
            return Result.Success(new CertificateChainValidationResult(
                errors.Count == 0,
                trustedRoot,
                chain.ToImmutableArray(),
                errors.ToImmutableArray()));
        }
        catch (Exception ex)
        {
            errors.Add($"Error during chain validation: {ex.Message}");
            return Result.Success(new CertificateChainValidationResult(
                false, Maybe<X509Certificate>.None, ImmutableArray<X509Certificate>.Empty,
                errors.ToImmutableArray()));
        }
    }

    /// <summary>
    /// Compares two certificate identities using Subject Key Identifier.
    /// </summary>
    public bool AreSameSignerIdentity(X509Certificate cert1, X509Certificate cert2)
    {
        try
        {
            _logger.LogDebug("Comparing signer identities:");
            _logger.LogDebug("  Cert1 Subject: {Subject1}", cert1.SubjectDN);
            _logger.LogDebug("  Cert2 Subject: {Subject2}", cert2.SubjectDN);
            
            // Primary comparison: Subject Key Identifier
            var ski1 = GetSubjectKeyIdentifier(cert1);
            var ski2 = GetSubjectKeyIdentifier(cert2);
            
            _logger.LogDebug("  Cert1 SKI: {SKI1}", ski1.HasValue ? Convert.ToHexString(ski1.Value.ToArray()) : "None");
            _logger.LogDebug("  Cert2 SKI: {SKI2}", ski2.HasValue ? Convert.ToHexString(ski2.Value.ToArray()) : "None");
            
            if (ski1.HasValue && ski2.HasValue)
            {
                var skiMatch = ski1.Value.SequenceEqual(ski2.Value);
                _logger.LogDebug("  SKI comparison result: {Match}", skiMatch);
                return skiMatch;
            }
            
            // Fallback: Subject DN comparison
            var subjectMatch = cert1.SubjectDN.Equals(cert2.SubjectDN);
            _logger.LogDebug("  Subject DN comparison result: {Match}", subjectMatch);
            return subjectMatch;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error comparing certificate identities, falling back to subject DN comparison");
            return cert1.SubjectDN.Equals(cert2.SubjectDN);
        }
    }

    /// <summary>
    /// Extracts the signing certificate from a CBEFF signature.
    /// </summary>
    private Maybe<X509Certificate> ExtractCbeffSignerCertificate(CbeffPayload cbeffPayload)
    {
        try
        {
            if (!cbeffPayload.Signature.HasValue)
                return Maybe<X509Certificate>.None;

            var signature = cbeffPayload.Signature.Value;
            var cmsSignedData = new CmsSignedData(signature.SignatureData);
            
            var certificates = cmsSignedData.GetCertificates();
            var certList = new List<X509Certificate>(certificates.EnumerateMatches(null));
            
            if (certList.Count == 0)
                return Maybe<X509Certificate>.None;

            // Return the first certificate (should be the signer)
            return Maybe<X509Certificate>.From(certList[0]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract CBEFF signer certificate");
            return Maybe<X509Certificate>.None;
        }
    }

    /// <summary>
    /// Extracts the signing certificate from a CHUID structure.
    /// </summary>
    private Maybe<X509Certificate> ExtractChuidSignerCertificate(ChuidStructure chuidStructure)
    {
        try
        {
            _logger.LogDebug("Extracting certificate from CHUID asymmetric signature");
            
            if (chuidStructure.AsymmetricSignature == null)
            {
                _logger.LogDebug("CHUID has no asymmetric signature");
                return Maybe<X509Certificate>.None;
            }

            _logger.LogDebug("Parsing CMS signed data from CHUID signature ({Length} bytes)", 
                chuidStructure.AsymmetricSignature.Length);
            
            var cmsSignedData = new CmsSignedData(chuidStructure.AsymmetricSignature);
            var certificates = cmsSignedData.GetCertificates();
            var certList = new List<X509Certificate>(certificates.EnumerateMatches(null));
            
            _logger.LogDebug("Found {Count} certificates in CHUID CMS signature", certList.Count);
            
            if (certList.Count == 0)
            {
                _logger.LogDebug("No certificates found in CHUID CMS signature");
                return Maybe<X509Certificate>.None;
            }

            return Maybe<X509Certificate>.From(certList[0]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract CHUID signer certificate");
            return Maybe<X509Certificate>.None;
        }
    }

    /// <summary>
    /// Finds a certificate in the trusted store that matches the given certificate.
    /// </summary>
    private Maybe<X509Certificate> FindInTrustedStore(X509Certificate certificate, ITrustedCertificateStoreExtended trustedStore)
    {
        try
        {
            // Try to find by fingerprint (most reliable)
            var fingerprint = trustedStore.CalculateFingerprint(certificate);
            var byFingerprintResult = trustedStore.FindByFingerprint(fingerprint);
            if (byFingerprintResult.IsSuccess && byFingerprintResult.Value.HasValue)
                return byFingerprintResult.Value;

            // Try by Subject Key Identifier
            var ski = GetSubjectKeyIdentifier(certificate);
            if (ski.HasValue)
            {
                var bySkiResult = trustedStore.FindBySubjectKeyIdentifier(ski.Value.ToArray());
                if (bySkiResult.IsSuccess && bySkiResult.Value.Any())
                    return Maybe<X509Certificate>.From(bySkiResult.Value.First());
            }

            // Try by serial number
            var bySerialResult = trustedStore.FindBySerialNumber(certificate.SerialNumber);
            if (bySerialResult.IsSuccess && bySerialResult.Value.Any())
                return Maybe<X509Certificate>.From(bySerialResult.Value.First());

            return Maybe<X509Certificate>.None;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error finding certificate in trusted store");
            return Maybe<X509Certificate>.None;
        }
    }

    /// <summary>
    /// Determines the validation strategy and recommended certificate per PIV specification.
    /// </summary>
    private (Result<X509Certificate> Certificate, CertificateValidationStrategy Strategy) DetermineValidationStrategy(
        Maybe<X509Certificate> cbeffCert,
        Maybe<X509Certificate> chuidCert,
        Maybe<X509Certificate> trustedCert,
        bool signerIdentityMatch)
    {
        _logger.LogDebug("Determining validation strategy:");
        _logger.LogDebug("  CBEFF cert available: {HasCbeff}", cbeffCert.HasValue);
        _logger.LogDebug("  CHUID cert available: {HasChuid}", chuidCert.HasValue);  
        _logger.LogDebug("  Trusted cert available: {HasTrusted}", trustedCert.HasValue);
        _logger.LogDebug("  Signer identity match: {Match}", signerIdentityMatch);
        
        // PIV Specification Rule: If signers match, use CHUID certificate
        if (signerIdentityMatch && chuidCert.HasValue)
        {
            _logger.LogDebug("Strategy: ChuidCertificate (signers match)");
            return (Result.Success(chuidCert.Value), CertificateValidationStrategy.ChuidCertificate);
        }

        // PIV Specification Rule: If signers don't match, use CBEFF embedded certificate
        if (chuidCert.HasValue && cbeffCert.HasValue && !signerIdentityMatch)
        {
            _logger.LogDebug("Strategy: CbeffCertificate (signers don't match)");
            return (Result.Success(cbeffCert.Value), CertificateValidationStrategy.CbeffCertificate);
        }

        // CBEFF only scenario
        if (cbeffCert.HasValue)
        {
            _logger.LogDebug("Strategy: CbeffOnly (no CHUID cert)");
            return (Result.Success(cbeffCert.Value), CertificateValidationStrategy.CbeffOnly);
        }

        // CHUID only scenario (common when CBEFF has no embedded certificates)
        if (chuidCert.HasValue)
        {
            _logger.LogDebug("Strategy: ChuidCertificate (no CBEFF cert available)");
            return (Result.Success(chuidCert.Value), CertificateValidationStrategy.ChuidCertificate);
        }

        // Last resort: trusted store certificate
        if (trustedCert.HasValue)
        {
            _logger.LogDebug("Strategy: TrustedStore (fallback)");
            return (Result.Success(trustedCert.Value), CertificateValidationStrategy.TrustedStore);
        }

        _logger.LogDebug("Strategy: FAILURE (no suitable certificate found)");
        return (Result.Failure<X509Certificate>("No suitable certificate found for validation"), 
                CertificateValidationStrategy.TrustedStore);
    }

    /// <summary>
    /// Builds a certificate chain from the given certificate to a trusted root.
    /// </summary>
    private Result<X509Certificate[]> BuildCertificateChain(X509Certificate certificate, ITrustedCertificateStoreExtended trustedStore)
    {
        _logger.LogDebug("Building certificate chain starting from: {Subject} (Serial: {Serial})", 
            certificate.SubjectDN, certificate.SerialNumber);
        
        var chain = new List<X509Certificate> { certificate };
        var current = certificate;

        // Build chain up to root
        while (!IsRootCertificate(current))
        {
            _logger.LogDebug("Finding issuer for: {Subject} (Issuer DN: {IssuerDN})", 
                current.SubjectDN, current.IssuerDN);
            
            var issuerResult = FindIssuerCertificate(current, trustedStore);
            if (issuerResult.IsFailure)
            {
                _logger.LogError("Could not find issuer for certificate: {Subject}", current.SubjectDN);
                return Result.Failure<X509Certificate[]>($"Could not find issuer for certificate: {current.SubjectDN}");
            }

            var issuer = issuerResult.Value;
            _logger.LogDebug("Found issuer: {Issuer} (Serial: {Serial})", issuer.SubjectDN, issuer.SerialNumber);
            
            if (chain.Contains(issuer))
            {
                _logger.LogError("Circular certificate chain detected at: {Issuer}", issuer.SubjectDN);
                return Result.Failure<X509Certificate[]>("Circular certificate chain detected");
            }

            chain.Add(issuer);
            current = issuer;
        }

        _logger.LogDebug("Built certificate chain with {Count} certificates", chain.Count);
        for (int i = 0; i < chain.Count; i++)
        {
            _logger.LogDebug("  [{Index}] {Subject}", i, chain[i].SubjectDN);
        }
        
        return Result.Success(chain.ToArray());
    }

    /// <summary>
    /// Finds the issuer certificate for the given certificate.
    /// </summary>
    private Result<X509Certificate> FindIssuerCertificate(X509Certificate certificate, ITrustedCertificateStoreExtended trustedStore)
    {
        try
        {
            // Get Authority Key Identifier
            var aki = GetAuthorityKeyIdentifier(certificate);
            if (aki.HasValue)
            {
                var byAkiResult = trustedStore.FindBySubjectKeyIdentifier(aki.Value.ToArray());
                if (byAkiResult.IsSuccess && byAkiResult.Value.Any())
                {
                    return Result.Success(byAkiResult.Value.First());
                }
            }

            // Fallback: find by issuer DN
            var byIssuerResult = trustedStore.FindBySubjectDN(certificate.IssuerDN.ToString());
            if (byIssuerResult.IsSuccess && byIssuerResult.Value.Any())
            {
                return Result.Success(byIssuerResult.Value.First());
            }

            return Result.Failure<X509Certificate>("Issuer certificate not found");
        }
        catch (Exception ex)
        {
            return Result.Failure<X509Certificate>($"Error finding issuer certificate: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines if a certificate is a root certificate (self-signed).
    /// </summary>
    private bool IsRootCertificate(X509Certificate certificate)
    {
        return certificate.SubjectDN.Equals(certificate.IssuerDN);
    }

    /// <summary>
    /// Extracts the Subject Key Identifier extension from a certificate.
    /// </summary>
    private Maybe<ImmutableArray<byte>> GetSubjectKeyIdentifier(X509Certificate certificate)
    {
        try
        {
            var skiExtension = certificate.GetExtensionValue(X509Extensions.SubjectKeyIdentifier);
            if (skiExtension != null)
            {
                var ski = SubjectKeyIdentifier.GetInstance(skiExtension.GetOctets());
                return Maybe<ImmutableArray<byte>>.From(ski.GetKeyIdentifier().ToImmutableArray());
            }
        }
        catch
        {
            // Ignore extraction errors
        }
        return Maybe<ImmutableArray<byte>>.None;
    }

    /// <summary>
    /// Extracts the Authority Key Identifier extension from a certificate.
    /// </summary>
    private Maybe<ImmutableArray<byte>> GetAuthorityKeyIdentifier(X509Certificate certificate)
    {
        try
        {
            var akiExtension = certificate.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier);
            if (akiExtension != null)
            {
                var aki = AuthorityKeyIdentifier.GetInstance(akiExtension.GetOctets());
                var keyId = aki.GetKeyIdentifier();
                if (keyId != null)
                {
                    return Maybe<ImmutableArray<byte>>.From(keyId.ToImmutableArray());
                }
            }
        }
        catch
        {
            // Ignore extraction errors
        }
        return Maybe<ImmutableArray<byte>>.None;
    }

    /// <summary>
    /// Checks if a certificate has the keyCertSign key usage extension.
    /// Required for certificates that sign other certificates (CAs).
    /// </summary>
    private bool HasKeyCertSignUsage(X509Certificate certificate)
    {
        try
        {
            var keyUsageExt = certificate.GetExtensionValue(X509Extensions.KeyUsage);
            if (keyUsageExt == null)
            {
                // No key usage extension - conservatively assume it can't sign certs
                return false;
            }

            var keyUsage = KeyUsage.GetInstance(keyUsageExt.GetOctets());
            // Check if the keyCertSign bit is set (bit 5)
            return (keyUsage.IntValue & KeyUsage.KeyCertSign) != 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking key usage for certificate {Subject}", certificate.SubjectDN);
            return false;
        }
    }
}