namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Interface for verifying CBEFF signatures.
/// </summary>
public interface ICbeffVerifier
{
    /// <summary>
    /// Verifies the signature on a signed CBEFF payload.
    /// </summary>
    /// <param name="payload">The CBEFF payload to verify</param>
    /// <param name="trustedCertificates">Collection of trusted certificates for verification</param>
    /// <returns>Result containing verification status and details</returns>
    Task<Result<CbeffVerificationResult>> VerifyAsync(
        CbeffPayload payload,
        X509Certificate2Collection trustedCertificates);

    /// <summary>
    /// Verifies a detached CMS signature for CBEFF data.
    /// </summary>
    /// <param name="data">The data that was signed</param>
    /// <param name="signature">The detached CMS signature</param>
    /// <param name="trustedCertificates">Collection of trusted certificates for verification</param>
    /// <returns>Result containing verification status and details</returns>
    Result<CbeffVerificationResult> VerifyDetachedSignature(
        byte[] data,
        byte[] signature,
        X509Certificate2Collection trustedCertificates);
}

/// <summary>
/// Result of CBEFF signature verification.
/// </summary>
/// <param name="IsValid">Whether the signature is valid</param>
/// <param name="SignerCertificate">The certificate used to sign the data</param>
/// <param name="SigningTime">When the signature was created</param>
/// <param name="CardUuid">Card UUID if present in signature attributes</param>
/// <param name="ValidationErrors">Any validation errors encountered</param>
public sealed record CbeffVerificationResult(
    bool IsValid,
    Maybe<X509Certificate2> SignerCertificate,
    Maybe<DateTime> SigningTime,
    Maybe<Guid> CardUuid,
    string[] ValidationErrors);