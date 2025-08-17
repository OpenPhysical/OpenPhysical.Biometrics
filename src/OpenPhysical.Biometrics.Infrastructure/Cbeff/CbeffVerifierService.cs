namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Domains.Cbeff;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System.Linq;
using Org.BouncyCastle.Asn1.Cms;

/// <summary>
/// Service for verifying CBEFF signatures using CMS.
/// </summary>
public class CbeffVerifierService : ICbeffVerifier
{
    /// <summary>
    /// Verifies the signature on a signed CBEFF payload.
    /// </summary>
    public async Task<Result<CbeffVerificationResult>> VerifyAsync(
        CbeffPayload payload,
        X509Certificate2Collection trustedCertificates)
    {
        if (!payload.Signature.HasValue)
        {
            return Result.Success(new CbeffVerificationResult(
                IsValid: false,
                SignerCertificate: Maybe<X509Certificate2>.None,
                SigningTime: Maybe<DateTime>.None,
                CardUuid: Maybe<Guid>.None,
                ValidationErrors: new[] { "CBEFF payload is not signed" }
            ));
        }

        try
        {
            // Get the data that was signed (header + BDB)
            var signedData = GetSignedData(payload);
            var signature = payload.Signature.Value;
            
            // Verify the detached signature
            return VerifyDetachedSignature(signedData, signature.SignatureData, trustedCertificates);
        }
        catch (Exception ex)
        {
            return Result.Success(new CbeffVerificationResult(
                IsValid: false,
                SignerCertificate: Maybe<X509Certificate2>.None,
                SigningTime: Maybe<DateTime>.None,
                CardUuid: Maybe<Guid>.None,
                ValidationErrors: new[] { $"Error during verification: {ex.Message}" }
            ));
        }
    }

    /// <summary>
    /// Verifies a detached CMS signature for CBEFF data.
    /// </summary>
    public Result<CbeffVerificationResult> VerifyDetachedSignature(
        byte[] data,
        byte[] signature,
        X509Certificate2Collection trustedCertificates)
    {
        var errors = new List<string>();
        
        try
        {
            // Parse the CMS signed data
            var cmsSignedData = new CmsSignedData(new CmsProcessableByteArray(data), signature);
            
            // Get signer information
            var signers = cmsSignedData.GetSignerInfos();
            if (signers.Count == 0)
            {
                errors.Add("No signers found in CMS signature");
                return CreateFailureResult(errors.ToArray());
            }
            
            if (signers.Count > 1)
            {
                errors.Add($"Multiple signers found ({signers.Count}), expected exactly one");
                return CreateFailureResult(errors.ToArray());
            }
            
            var signerInfo = signers.Cast<SignerInformation>().First();
            
            // Find the signing certificate
            var signerCertResult = FindSigningCertificate(signerInfo, trustedCertificates);
            if (signerCertResult.IsFailure)
            {
                errors.Add(signerCertResult.Error);
                return CreateFailureResult(errors.ToArray());
            }
            
            var signerCert = signerCertResult.Value;
            
            // Convert to BouncyCastle certificate for verification
            var bcCert = DotNetUtilities.FromX509Certificate(signerCert);
            
            // Verify the signature
            bool isValid;
            try
            {
                isValid = signerInfo.Verify(bcCert);
            }
            catch (Exception ex)
            {
                errors.Add($"Signature verification failed: {ex.Message}");
                return CreateFailureResult(errors.ToArray());
            }
            
            if (!isValid)
            {
                errors.Add("Digital signature verification failed");
                return CreateFailureResult(errors.ToArray());
            }
            
            // Extract signing time if present
            var signingTime = ExtractSigningTime(signerInfo);
            
            // Extract card UUID if present
            var cardUuid = ExtractCardUuid(signerInfo);
            
            // Perform additional validations
            var additionalValidationErrors = PerformAdditionalValidations(signerInfo, signerCert);
            errors.AddRange(additionalValidationErrors);
            
            return Result.Success(new CbeffVerificationResult(
                IsValid: errors.Count == 0,
                SignerCertificate: Maybe<X509Certificate2>.From(signerCert),
                SigningTime: signingTime,
                CardUuid: cardUuid,
                ValidationErrors: errors.ToArray()
            ));
        }
        catch (Exception ex)
        {
            errors.Add($"Error parsing or verifying CMS signature: {ex.Message}");
            return CreateFailureResult(errors.ToArray());
        }
    }

    /// <summary>
    /// Finds the signing certificate in the trusted certificate collection.
    /// </summary>
    private Result<X509Certificate2> FindSigningCertificate(
        SignerInformation signerInfo,
        X509Certificate2Collection trustedCertificates)
    {
        try
        {
            var signerId = signerInfo.SignerID;
            
            // Try to find by issuer and serial number
            if (signerId.Issuer != null && signerId.SerialNumber != null)
            {
                foreach (X509Certificate2 cert in trustedCertificates)
                {
                    var bcCert = DotNetUtilities.FromX509Certificate(cert);
                    if (signerId.Match(bcCert))
                    {
                        return Result.Success(cert);
                    }
                }
            }
            
            // Try to find by subject key identifier
            if (signerId.SubjectKeyIdentifier != null)
            {
                var ski = signerId.SubjectKeyIdentifier;
                foreach (X509Certificate2 cert in trustedCertificates)
                {
                    var certSki = cert.Extensions.OfType<X509SubjectKeyIdentifierExtension>().FirstOrDefault()?.SubjectKeyIdentifier;
                    if (certSki != null && ski.SequenceEqual(Convert.FromHexString(certSki)))
                    {
                        return Result.Success(cert);
                    }
                }
            }
            
            return Result.Failure<X509Certificate2>("Signing certificate not found in trusted certificate collection");
        }
        catch (Exception ex)
        {
            return Result.Failure<X509Certificate2>($"Error finding signing certificate: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the signing time from signed attributes if present.
    /// </summary>
    private Maybe<DateTime> ExtractSigningTime(SignerInformation signerInfo)
    {
        try
        {
            var signedAttributes = signerInfo.SignedAttributes;
            if (signedAttributes == null)
                return Maybe<DateTime>.None;
            
            var signingTimeAttr = signedAttributes[CmsAttributes.SigningTime];
            if (signingTimeAttr != null && signingTimeAttr.AttrValues.Count > 0)
            {
                var timeValue = signingTimeAttr.AttrValues[0];
                if (timeValue is Org.BouncyCastle.Asn1.Cms.Time time)
                {
                    return Maybe<DateTime>.From(time.Date);
                }
            }
            
            return Maybe<DateTime>.None;
        }
        catch
        {
            return Maybe<DateTime>.None;
        }
    }

    /// <summary>
    /// Extracts the card UUID from signed attributes if present.
    /// </summary>
    private Maybe<Guid> ExtractCardUuid(SignerInformation signerInfo)
    {
        try
        {
            var signedAttributes = signerInfo.SignedAttributes;
            if (signedAttributes == null)
                return Maybe<Guid>.None;
            
            // Look for the card UUID attribute (custom OID)
            var cardUuidOid = "2.16.840.1.101.3.6.9.1"; // This should match the OID used in signing
            var cardUuidAttr = signedAttributes[new Org.BouncyCastle.Asn1.DerObjectIdentifier(cardUuidOid)];
            
            if (cardUuidAttr != null && cardUuidAttr.AttrValues.Count > 0)
            {
                var uuidValue = cardUuidAttr.AttrValues[0];
                if (uuidValue is Org.BouncyCastle.Asn1.DerOctetString octetString)
                {
                    var guidBytes = octetString.GetOctets();
                    if (guidBytes.Length == 16)
                    {
                        return Maybe<Guid>.From(new Guid(guidBytes));
                    }
                }
            }
            
            return Maybe<Guid>.None;
        }
        catch
        {
            return Maybe<Guid>.None;
        }
    }

    /// <summary>
    /// Performs additional PIV-specific validations.
    /// </summary>
    private List<string> PerformAdditionalValidations(SignerInformation signerInfo, X509Certificate2 signerCert)
    {
        var errors = new List<string>();
        
        try
        {
            // Check certificate validity period
            var now = DateTime.UtcNow;
            if (now < signerCert.NotBefore || now > signerCert.NotAfter)
            {
                errors.Add($"Signing certificate is not valid at current time (valid from {signerCert.NotBefore} to {signerCert.NotAfter})");
            }
            
            // Check if certificate has required key usage for digital signatures
            var keyUsageExt = signerCert.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
            if (keyUsageExt != null)
            {
                if (!keyUsageExt.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
                {
                    errors.Add("Signing certificate does not have digital signature key usage");
                }
            }
            
            // Validate digest algorithm
            var digestAlgorithm = signerInfo.DigestAlgOid;
            if (digestAlgorithm != "2.16.840.1.101.3.4.2.1") // SHA-256
            {
                errors.Add($"Unsupported digest algorithm: {digestAlgorithm} (expected SHA-256)");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error during additional validation: {ex.Message}");
        }
        
        return errors;
    }

    /// <summary>
    /// Gets the data that was signed from a CBEFF payload.
    /// </summary>
    private byte[] GetSignedData(CbeffPayload payload)
    {
        var headerBytes = CbeffHeaderSerializer.ToBytes(payload.Header);
        var bdbBytes = payload.GetBiometricDataBlock();
        
        var signedData = new byte[headerBytes.Length + bdbBytes.Length];
        Array.Copy(headerBytes, 0, signedData, 0, headerBytes.Length);
        Array.Copy(bdbBytes, 0, signedData, headerBytes.Length, bdbBytes.Length);
        
        return signedData;
    }

    /// <summary>
    /// Creates a failure result with the given errors.
    /// </summary>
    private Result<CbeffVerificationResult> CreateFailureResult(string[] errors)
    {
        return Result.Success(new CbeffVerificationResult(
            IsValid: false,
            SignerCertificate: Maybe<X509Certificate2>.None,
            SigningTime: Maybe<DateTime>.None,
            CardUuid: Maybe<Guid>.None,
            ValidationErrors: errors
        ));
    }
}