namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Domains.Cbeff;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Security;
using System.Linq;

/// <summary>
/// Service for signing CBEFF payloads using CMS (Cryptographic Message Syntax).
/// Based on the signing logic from SignedDataObject.cs in OpenPhysical.Net.
/// </summary>
public class CbeffSignerService : ICbeffSigner
{
    private const string SignatureAlgorithm = "SHA256withECDSA";
    private const string DigestAlgorithm = "SHA-256";

    /// <summary>
    /// Signs a CBEFF payload using CMS.
    /// </summary>
    public async Task<Result<CbeffPayload>> SignAsync(
        CbeffPayload payload,
        X509Certificate2 signingCertificate,
        Maybe<Guid> includeCardUuid = default)
    {
        try
        {
            // Get the data to sign (header + BDB)
            var dataToSign = GetDataToSign(payload);
            
            // Create the signature
            var signatureResult = CreateDetachedSignature(dataToSign, signingCertificate, includeCardUuid);
            if (signatureResult.IsFailure)
                return Result.Failure<CbeffPayload>(signatureResult.Error);
            
            var signatureBytes = signatureResult.Value;
            
            // Create the signature object
            var cbeffSignatureResult = CbeffSignature.Create(signatureBytes);
            if (cbeffSignatureResult.IsFailure)
                return Result.Failure<CbeffPayload>(cbeffSignatureResult.Error);
            
            // Return new payload with signature
            return Result.Success(payload.WithSignature(cbeffSignatureResult.Value));
        }
        catch (Exception ex)
        {
            return Result.Failure<CbeffPayload>($"Error signing CBEFF payload: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a detached CMS signature for CBEFF data.
    /// </summary>
    public Result<byte[]> CreateDetachedSignature(
        byte[] dataToSign,
        X509Certificate2 signingCertificate,
        Maybe<Guid> includeCardUuid = default)
    {
        try
        {
            // Convert .NET certificate to BouncyCastle
            var bcCert = DotNetUtilities.FromX509Certificate(signingCertificate);
            var privateKey = DotNetUtilities.GetKeyPair(signingCertificate.PrivateKey).Private;
            
            // Create CMS signed data generator
            var cmsGenerator = new CmsSignedDataGenerator();
            var contentSigner = new Asn1SignatureFactory(SignatureAlgorithm, privateKey);
            
            if (includeCardUuid.HasValue)
            {
                // Add card UUID as signed attribute
                var cardUuidBytes = includeCardUuid.Value.ToByteArray();
                var customAttribute = new DerTaggedObject(false, 0, new DerOctetString(cardUuidBytes));
                var attributes = new Asn1EncodableVector();
                
                // Use a custom OID for card UUID (from Constants in original code)
                var cardUuidOid = new DerObjectIdentifier("2.16.840.1.101.3.6.9.1"); // Placeholder OID
                attributes.Add(new Org.BouncyCastle.Asn1.Cms.Attribute(cardUuidOid, new DerSet(customAttribute)));
                var attributeTable = new AttributeTable(new DerSet(attributes));
                
                var signerInfoGenerator = new SignerInfoGeneratorBuilder()
                    .WithSignedAttributeGenerator(new DefaultSignedAttributeTableGenerator(attributeTable))
                    .Build(contentSigner, bcCert);
                    
                cmsGenerator.AddSignerInfoGenerator(signerInfoGenerator);
            }
            else
            {
                // Standard signing without custom attributes
                cmsGenerator.AddSigner(privateKey, bcCert, CmsSignedGenerator.DigestSha256);
            }
            
            // Create the content to sign
            var content = new CmsProcessableByteArray(dataToSign);
            
            // For CBEFF, we need to try multiple times to get consistent signature size
            // This is based on the CalculateValidSignature method from the original code
            var targetSignatureSize = EstimateSignatureSize(signingCertificate);
            
            for (int attempt = 0; attempt < 32; attempt++)
            {
                var cmsSignedData = cmsGenerator.Generate(
                    CmsSignedGenerator.Data, // Content type
                    content, 
                    false); // Don't encapsulate (detached signature)
                    
                var signatureBytes = cmsSignedData.GetEncoded("DER");
                
                if (signatureBytes.Length == targetSignatureSize)
                {
                    return Result.Success(signatureBytes);
                }
            }
            
            // If we couldn't get the exact size, return the last attempt
            var finalSignature = cmsGenerator.Generate(CmsSignedGenerator.Data, content, false);
            return Result.Success(finalSignature.GetEncoded("DER"));
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>($"Error creating detached signature: {ex.Message}");
        }
    }

    /// <summary>
    /// Estimates the signature size for a given certificate.
    /// </summary>
    public ushort EstimateSignatureSize(X509Certificate2 signingCertificate)
    {
        try
        {
            // Create a dummy signature to estimate size
            var dummyData = new byte[100]; // Small dummy data
            var dummySignatureResult = CreateDetachedSignature(dummyData, signingCertificate);
            
            if (dummySignatureResult.IsSuccess)
            {
                return (ushort)dummySignatureResult.Value.Length;
            }
            
            // Fallback estimates based on key type/size
            var keyAlgorithm = signingCertificate.PublicKey.Oid.Value;
            
            return keyAlgorithm switch
            {
                "1.2.840.10045.2.1" => 256, // ECDSA - typical size
                "1.2.840.113549.1.1.1" => GetRsaSignatureSize(signingCertificate), // RSA
                _ => 256 // Default estimate
            };
        }
        catch
        {
            return 256; // Safe default
        }
    }

    /// <summary>
    /// Gets the data that should be signed for a CBEFF payload.
    /// For PIV, this is typically the header + BDB.
    /// </summary>
    private byte[] GetDataToSign(CbeffPayload payload)
    {
        var headerBytes = CbeffHeaderSerializer.ToBytes(payload.Header);
        var bdbBytes = payload.GetBiometricDataBlock();
        
        var dataToSign = new byte[headerBytes.Length + bdbBytes.Length];
        Array.Copy(headerBytes, 0, dataToSign, 0, headerBytes.Length);
        Array.Copy(bdbBytes, 0, dataToSign, headerBytes.Length, bdbBytes.Length);
        
        return dataToSign;
    }

    /// <summary>
    /// Estimates RSA signature size based on key size.
    /// </summary>
    private ushort GetRsaSignatureSize(X509Certificate2 certificate)
    {
        var keySize = certificate.PublicKey.Key.KeySize;
        
        return keySize switch
        {
            1024 => 384,  // RSA-1024 typical CMS signature size
            2048 => 512,  // RSA-2048 typical CMS signature size
            3072 => 640,  // RSA-3072 typical CMS signature size
            4096 => 768,  // RSA-4096 typical CMS signature size
            _ => (ushort)(keySize / 8 + 128) // Rough estimate with padding
        };
    }
}