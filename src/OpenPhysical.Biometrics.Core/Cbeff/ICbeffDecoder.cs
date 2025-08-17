namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff;

/// <summary>
/// Interface for decoding CBEFF format biometric data.
/// </summary>
public interface ICbeffDecoder
{
    /// <summary>
    /// Decodes a CBEFF payload from raw bytes.
    /// </summary>
    /// <param name="cbeffData">The raw CBEFF data bytes</param>
    /// <returns>Result containing the decoded CBEFF payload or error</returns>
    Task<Result<CbeffPayload>> DecodeAsync(byte[] cbeffData);

    /// <summary>
    /// Validates a CBEFF payload structure without fully decoding.
    /// </summary>
    /// <param name="cbeffData">The raw CBEFF data bytes</param>
    /// <returns>Result indicating if the data is valid CBEFF format</returns>
    Result<bool> ValidateStructure(byte[] cbeffData);

    /// <summary>
    /// Extracts just the biometric data block from CBEFF data.
    /// </summary>
    /// <param name="cbeffData">The raw CBEFF data bytes</param>
    /// <returns>Result containing the biometric data block or error</returns>
    Result<byte[]> ExtractBiometricData(byte[] cbeffData);
}