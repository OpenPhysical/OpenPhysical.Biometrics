namespace OpenPhysical.Biometrics.Core.Cbeff;

using System;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Cbeff;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

/// <summary>
/// Interface for building CBEFF containers from biometric data.
/// </summary>
public interface ICbeffBuilder
{
    /// <summary>
    /// Creates a CBEFF container from formatted biometric data.
    /// </summary>
    /// <param name="biometricData">The formatted biometric data (e.g., INCITS 385)</param>
    /// <param name="modality">The biometric modality</param>
    /// <param name="format">The biometric format specification</param>
    /// <param name="metadata">CBEFF metadata including FASC-N and dates</param>
    /// <returns>Result containing the CBEFF payload or error</returns>
    Task<Result<CbeffPayload>> BuildAsync(
        byte[] biometricData,
        BiometricModality modality,
        BiometricFormat format,
        CbeffMetadata metadata
    );

    /// <summary>
    /// Creates a signed CBEFF container from formatted biometric data.
    /// </summary>
    /// <param name="biometricData">The formatted biometric data (e.g., INCITS 385)</param>
    /// <param name="modality">The biometric modality</param>
    /// <param name="format">The biometric format specification</param>
    /// <param name="metadata">CBEFF metadata including FASC-N and dates</param>
    /// <param name="signingCertificate">Certificate for signing</param>
    /// <param name="privateKey">The private key corresponding to the signing certificate</param>
    /// <param name="includeCardUuid">Optional card UUID to include in signature</param>
    /// <returns>Result containing the signed CBEFF payload or error</returns>
    Task<Result<CbeffPayload>> BuildSignedAsync(
        byte[] biometricData,
        BiometricModality modality,
        BiometricFormat format,
        CbeffMetadata metadata,
        X509Certificate signingCertificate,
        AsymmetricKeyParameter privateKey,
        Maybe<Guid> includeCardUuid = default
    );
}
