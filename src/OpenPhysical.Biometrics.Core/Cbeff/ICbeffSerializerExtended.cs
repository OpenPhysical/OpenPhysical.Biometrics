namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using System.Collections.Immutable;

/// <summary>
/// Extended CBEFF serializer that uses format registry for parsing biometric data.
/// </summary>
public interface ICbeffSerializerExtended : ICbeffSerializer
{
    /// <summary>
    /// Deserializes CBEFF data and parses the biometric content using registered format handlers.
    /// </summary>
    /// <param name="data">The CBEFF data to deserialize.</param>
    /// <returns>An extended payload with parsed biometric data if a format handler is available.</returns>
    Result<CbeffPayloadExtended> DeserializeExtended(ImmutableArray<byte> data);
    
    /// <summary>
    /// Deserializes CBEFF data and parses the biometric content using registered format handlers.
    /// </summary>
    /// <param name="data">The CBEFF data to deserialize.</param>
    /// <returns>An extended payload with parsed biometric data if a format handler is available.</returns>
    Result<CbeffPayloadExtended> DeserializeExtended(byte[] data);
    
    /// <summary>
    /// Deserializes CBEFF data with automatic PIV wrapper detection and parses biometric content.
    /// </summary>
    /// <param name="data">The CBEFF data to deserialize.</param>
    /// <returns>An extended payload with parsed biometric data if a format handler is available.</returns>
    Result<CbeffPayloadExtended> DeserializeAutoExtended(ImmutableArray<byte> data);
    
    /// <summary>
    /// Deserializes CBEFF data with automatic PIV wrapper detection and parses biometric content.
    /// </summary>
    /// <param name="data">The CBEFF data to deserialize.</param>
    /// <returns>An extended payload with parsed biometric data if a format handler is available.</returns>
    Result<CbeffPayloadExtended> DeserializeAutoExtended(byte[] data);
}