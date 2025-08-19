namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Domains.Cbeff;
using System.Collections.Immutable;

/// <summary>
/// Service for performing comprehensive PIV CBEFF-CHUID cross-validation.
/// Implements FIPS 201-3 requirements for PIV biometric signature validation.
/// </summary>
public interface IPivCrossValidationService
{
    /// <summary>
    /// Performs comprehensive cross-validation between CBEFF and CHUID signatures.
    /// </summary>
    /// <param name="cbeffPayload">The CBEFF payload to validate</param>
    /// <param name="chuidFilePath">Optional path to CHUID file (5FC1A4.dat)</param>
    /// <param name="trustedStore">Trusted certificate store for validation</param>
    /// <returns>Result containing comprehensive validation results</returns>
    Task<Result<PivCrossValidationResult>> ValidateAsync(
        CbeffPayload cbeffPayload,
        Maybe<string> chuidFilePath,
        ITrustedCertificateStoreExtended trustedStore);

    /// <summary>
    /// Attempts to auto-detect CHUID file in the same directory as a CBEFF file.
    /// </summary>
    /// <param name="cbeffFilePath">Path to the CBEFF file</param>
    /// <returns>Path to CHUID file if found</returns>
    Maybe<string> AutoDetectChuidFile(string cbeffFilePath);
}

/// <summary>
/// Comprehensive result of PIV cross-validation between CBEFF and CHUID.
/// </summary>
/// <param name="IsValid">Overall validation result</param>
/// <param name="CbeffValidation">CBEFF-specific validation results</param>
/// <param name="ChuidValidation">CHUID-specific validation results (if available)</param>
/// <param name="CertificateDiscovery">Certificate discovery and selection results</param>
/// <param name="ChainValidation">Certificate chain validation results</param>
/// <param name="CrossValidationChecks">Cross-validation specific checks</param>
/// <param name="ValidationStrategy">Strategy used for validation</param>
/// <param name="ValidationErrors">Comprehensive list of validation errors</param>
public sealed record PivCrossValidationResult(
    bool IsValid,
    CbeffVerificationResult CbeffValidation,
    Maybe<ChuidValidationResult> ChuidValidation,
    CertificateDiscoveryResult CertificateDiscovery,
    CertificateChainValidationResult ChainValidation,
    CrossValidationChecks CrossValidationChecks,
    CertificateValidationStrategy ValidationStrategy,
    ImmutableArray<string> ValidationErrors);

/// <summary>
/// CHUID-specific validation results.
/// </summary>
/// <param name="ChuidStructure">Parsed CHUID structure</param>
/// <param name="SignatureValid">Whether CHUID signature is valid</param>
/// <param name="SignerCertificate">Certificate used to sign CHUID</param>
/// <param name="ValidationErrors">CHUID-specific validation errors</param>
public sealed record ChuidValidationResult(
    ChuidStructure ChuidStructure,
    bool SignatureValid,
    Maybe<Org.BouncyCastle.X509.X509Certificate> SignerCertificate,
    ImmutableArray<string> ValidationErrors);

/// <summary>
/// Cross-validation checks between CBEFF and CHUID.
/// </summary>
/// <param name="FascNMatches">Whether FASC-N in CBEFF header matches CHUID FASC-N</param>
/// <param name="SignerIdentityMatches">Whether CBEFF and CHUID signers represent same identity</param>
/// <param name="CardUuidMatches">Whether Card UUID matches (if present in both)</param>
/// <param name="ValidationTimestampConsistency">Whether validation timestamps are consistent</param>
public sealed record CrossValidationChecks(
    bool FascNMatches,
    bool SignerIdentityMatches,
    bool CardUuidMatches,
    bool ValidationTimestampConsistency);