namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Cbeff;

/// <summary>
/// Interface for encoding biometric data into CBEFF format.
/// </summary>
public interface ICbeffEncoder
{
    /// <summary>
    /// Encodes biometric data into a CBEFF payload.
    /// </summary>
    /// <param name="biometricData">The raw biometric data to encode</param>
    /// <param name="modality">The biometric modality (face, fingerprint, etc.)</param>
    /// <param name="format">The biometric format to use</param>
    /// <param name="metadata">Additional metadata for the CBEFF header</param>
    /// <returns>Result containing the encoded CBEFF payload or error</returns>
    Task<Result<CbeffPayload>> EncodeAsync(
        byte[] biometricData,
        BiometricModality modality,
        BiometricFormat format,
        CbeffMetadata metadata);

    /// <summary>
    /// Creates a CBEFF header for the given parameters.
    /// </summary>
    /// <param name="biometricDataLength">Length of the biometric data block</param>
    /// <param name="signatureLength">Length of the signature block (0 if unsigned)</param>
    /// <param name="modality">The biometric modality</param>
    /// <param name="format">The biometric format</param>
    /// <param name="metadata">Additional metadata</param>
    /// <returns>Result containing the CBEFF header or error</returns>
    Result<CbeffHeader> CreateHeader(
        uint biometricDataLength,
        ushort signatureLength,
        BiometricModality modality,
        BiometricFormat format,
        CbeffMetadata metadata);
}