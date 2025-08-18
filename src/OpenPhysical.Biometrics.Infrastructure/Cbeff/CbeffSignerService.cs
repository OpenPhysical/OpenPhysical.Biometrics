namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Domains.Cbeff;
using System;
using System.Threading.Tasks;
using Org.BouncyCastle.X509;

/// <summary>
/// Service for signing CBEFF payloads using CMS (Cryptographic Message Syntax).
/// Based on the signing logic from SignedDataObject.cs in OpenPhysical.Net.
/// </summary>
public class CbeffSignerService : ICbeffSigner
{
    /// <summary>
    /// Signs a CBEFF payload using CMS.
    /// </summary>
    public async Task<Result<CbeffPayload>> SignAsync(
        CbeffPayload payload,
        X509Certificate signingCertificate,
        Maybe<Guid> includeCardUuid = default)
    {
        // TODO: Implement BouncyCastle-based signing
        await Task.CompletedTask;
        return Result.Failure<CbeffPayload>("CbeffSignerService not yet implemented for BouncyCastle certificates");
    }

    /// <summary>
    /// Creates a detached CMS signature for CBEFF data.
    /// </summary>
    public Result<byte[]> CreateDetachedSignature(
        byte[] dataToSign,
        X509Certificate signingCertificate,
        Maybe<Guid> includeCardUuid = default)
    {
        // TODO: Implement BouncyCastle-based signing 
        return Result.Failure<byte[]>("CbeffSignerService not yet implemented for BouncyCastle certificates");
    }

    /// <summary>
    /// Calculates the signature size for a given certificate.
    /// This is needed for pre-allocating space in the CBEFF header.
    /// </summary>
    public ushort EstimateSignatureSize(X509Certificate signingCertificate)
    {
        // TODO: Implement BouncyCastle-based signature size estimation
        return 1024; // Reasonable default for testing
    }
}