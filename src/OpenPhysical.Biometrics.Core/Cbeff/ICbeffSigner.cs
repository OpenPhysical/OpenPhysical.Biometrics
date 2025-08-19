namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Crypto;

/// <summary>
/// Interface for signing CBEFF payloads.
/// </summary>
public interface ICbeffSigner
{
    /// <summary>
    /// Signs a CBEFF payload using CMS (Cryptographic Message Syntax).
    /// </summary>
    /// <param name="payload">The CBEFF payload to sign</param>
    /// <param name="signingCertificate">The certificate to use for signing</param>
    /// <param name="privateKey">The private key corresponding to the signing certificate</param>
    /// <param name="includeCardUuid">Optional card UUID to include in signature attributes</param>
    /// <returns>Result containing the signed CBEFF payload or error</returns>
    Task<Result<CbeffPayload>> SignAsync(
        CbeffPayload payload,
        X509Certificate signingCertificate,
        AsymmetricKeyParameter privateKey,
        Maybe<Guid> includeCardUuid = default);

    /// <summary>
    /// Creates a detached CMS signature for CBEFF data.
    /// </summary>
    /// <param name="dataToSign">The data to sign (CBEFF header + BDB)</param>
    /// <param name="signingCertificate">The certificate to use for signing</param>
    /// <param name="privateKey">The private key corresponding to the signing certificate</param>
    /// <param name="includeCardUuid">Optional card UUID to include in signature attributes</param>
    /// <returns>Result containing the CMS signature bytes or error</returns>
    Result<byte[]> CreateDetachedSignature(
        byte[] dataToSign,
        X509Certificate signingCertificate,
        AsymmetricKeyParameter privateKey,
        Maybe<Guid> includeCardUuid = default);

    /// <summary>
    /// Calculates the signature size for a given certificate and private key.
    /// This is needed for pre-allocating space in the CBEFF header.
    /// </summary>
    /// <param name="signingCertificate">The certificate that will be used for signing</param>
    /// <param name="privateKey">The private key corresponding to the signing certificate</param>
    /// <returns>Estimated signature size in bytes</returns>
    ushort EstimateSignatureSize(
        X509Certificate signingCertificate,
        AsymmetricKeyParameter privateKey);
}