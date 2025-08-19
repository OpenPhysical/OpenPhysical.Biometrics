namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Domains.Cbeff;
using Org.BouncyCastle.X509;
using System.Collections.Immutable;

/// <summary>
/// Service for discovering and validating certificates from multiple sources.
/// Supports certificate discovery from trusted stores, CBEFF signatures, and CHUID structures.
/// </summary>
public interface ICertificateDiscoveryService
{
    /// <summary>
    /// Discovers all available certificates for a CBEFF signature verification.
    /// </summary>
    /// <param name="cbeffPayload">The CBEFF payload with signature</param>
    /// <param name="chuidStructure">Optional CHUID structure from the same PIV card</param>
    /// <param name="trustedStore">Trusted certificate store for chain validation</param>
    /// <returns>Result containing certificate discovery information</returns>
    Result<CertificateDiscoveryResult> DiscoverCertificates(
        CbeffPayload cbeffPayload,
        Maybe<ChuidStructure> chuidStructure,
        ITrustedCertificateStoreExtended trustedStore);

    /// <summary>
    /// Validates a certificate chain back to a trusted root.
    /// </summary>
    /// <param name="certificate">The certificate to validate</param>
    /// <param name="trustedStore">Trusted certificate store containing roots</param>
    /// <returns>Result containing chain validation status and details</returns>
    Result<CertificateChainValidationResult> ValidateCertificateChain(
        X509Certificate certificate,
        ITrustedCertificateStoreExtended trustedStore);

    /// <summary>
    /// Compares two certificate identities to determine if they represent the same signer.
    /// Uses Subject Key Identifier as the primary comparison method.
    /// </summary>
    /// <param name="cert1">First certificate</param>
    /// <param name="cert2">Second certificate</param>
    /// <returns>True if certificates represent the same signer identity</returns>
    bool AreSameSignerIdentity(X509Certificate cert1, X509Certificate cert2);
}

/// <summary>
/// Result of certificate discovery from multiple sources.
/// </summary>
/// <param name="CbeffSignerCertificate">Certificate from CBEFF signature (if embedded)</param>
/// <param name="ChuidSignerCertificate">Certificate from CHUID signature (if available)</param>
/// <param name="TrustedStoreCertificate">Matching certificate from trusted store (if found)</param>
/// <param name="RecommendedCertificate">The certificate recommended for validation based on PIV rules</param>
/// <param name="SignerIdentityMatch">Whether CBEFF and CHUID signers represent the same identity</param>
/// <param name="ValidationStrategy">The strategy used to determine the recommended certificate</param>
public sealed record CertificateDiscoveryResult(
    Maybe<X509Certificate> CbeffSignerCertificate,
    Maybe<X509Certificate> ChuidSignerCertificate,
    Maybe<X509Certificate> TrustedStoreCertificate,
    X509Certificate RecommendedCertificate,
    bool SignerIdentityMatch,
    CertificateValidationStrategy ValidationStrategy);

/// <summary>
/// Result of certificate chain validation.
/// </summary>
/// <param name="IsValid">Whether the certificate chain is valid</param>
/// <param name="TrustedRoot">The trusted root certificate used for validation</param>
/// <param name="ChainPath">The complete certificate chain from end-entity to root</param>
/// <param name="ValidationErrors">Any validation errors encountered</param>
public sealed record CertificateChainValidationResult(
    bool IsValid,
    Maybe<X509Certificate> TrustedRoot,
    ImmutableArray<X509Certificate> ChainPath,
    ImmutableArray<string> ValidationErrors);

/// <summary>
/// Strategy used for certificate validation in PIV cross-validation.
/// </summary>
public enum CertificateValidationStrategy
{
    /// <summary>
    /// CBEFF and CHUID signers match; using CHUID certificate per PIV specification.
    /// </summary>
    ChuidCertificate,

    /// <summary>
    /// CBEFF and CHUID signers differ; using CBEFF embedded certificate per PIV specification.
    /// </summary>
    CbeffCertificate,

    /// <summary>
    /// Only CBEFF signature available; using CBEFF embedded certificate.
    /// </summary>
    CbeffOnly,

    /// <summary>
    /// Using certificate from trusted store (fallback scenario).
    /// </summary>
    TrustedStore
}