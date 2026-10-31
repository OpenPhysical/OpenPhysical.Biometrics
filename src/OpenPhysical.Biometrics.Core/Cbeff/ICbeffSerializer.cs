namespace OpenPhysical.Biometrics.Core.Cbeff;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff;

/// <summary>
/// Defines the interface for CBEFF (Common Biometric Exchange Formats Framework) serialization.
/// </summary>
/// <remarks>
/// This interface provides a single responsibility: converting between CBEFF domain objects
/// and their binary representation. It handles both raw CBEFF data and PIV-wrapped CBEFF
/// (with BER/DER tag 0xBC) transparently.
/// </remarks>
public interface ICbeffSerializer
{
    /// <summary>
    /// Deserializes CBEFF data from its binary representation.
    /// </summary>
    /// <param name="data">The binary CBEFF data, possibly with PIV wrapper</param>
    /// <returns>A Result containing the deserialized CBEFF payload or an error</returns>
    Result<CbeffPayload> Deserialize(ImmutableArray<byte> data);

    /// <summary>
    /// Deserializes CBEFF data from a byte array.
    /// </summary>
    /// <param name="data">The binary CBEFF data, possibly with PIV wrapper</param>
    /// <returns>A Result containing the deserialized CBEFF payload or an error</returns>
    Result<CbeffPayload> Deserialize(byte[] data);

    /// <summary>
    /// Deserializes CBEFF data with automatic PIV wrapper detection.
    /// This method can handle both raw CBEFF data and PIV-wrapped CBEFF data.
    /// </summary>
    /// <param name="data">The binary CBEFF data</param>
    /// <returns>A Result containing the deserialized CBEFF payload or an error</returns>
    Result<CbeffPayload> DeserializeAuto(ImmutableArray<byte> data);

    /// <summary>
    /// Deserializes CBEFF data with automatic PIV wrapper detection.
    /// This method can handle both raw CBEFF data and PIV-wrapped CBEFF data.
    /// </summary>
    /// <param name="data">The binary CBEFF data</param>
    /// <returns>A Result containing the deserialized CBEFF payload or an error</returns>
    Result<CbeffPayload> DeserializeAuto(byte[] data);

    /// <summary>
    /// Serializes a CBEFF payload to its binary representation.
    /// </summary>
    /// <param name="payload">The CBEFF payload to serialize</param>
    /// <returns>A Result containing the serialized bytes or an error</returns>
    Result<ImmutableArray<byte>> Serialize(CbeffPayload payload);

    /// <summary>
    /// Serializes a CBEFF payload to a byte array.
    /// </summary>
    /// <param name="payload">The CBEFF payload to serialize</param>
    /// <returns>A Result containing the serialized bytes or an error</returns>
    Result<byte[]> SerializeToArray(CbeffPayload payload);

    /// <summary>
    /// Serializes a CBEFF payload with a BER/DER wrapper tag.
    /// </summary>
    /// <param name="payload">The CBEFF payload to serialize</param>
    /// <param name="tag">The BER/DER tag to wrap with (e.g., 0xBC for PIV biometric template)</param>
    /// <returns>A Result containing the wrapped serialized bytes or an error</returns>
    Result<ImmutableArray<byte>> SerializeWithWrapper(CbeffPayload payload, byte tag);

    /// <summary>
    /// Serializes a CBEFF payload with a BER/DER wrapper tag to a byte array.
    /// </summary>
    /// <param name="payload">The CBEFF payload to serialize</param>
    /// <param name="tag">The BER/DER tag to wrap with (e.g., 0xBC for PIV biometric template)</param>
    /// <returns>A Result containing the wrapped serialized bytes or an error</returns>
    Result<byte[]> SerializeWithWrapperToArray(CbeffPayload payload, byte tag);

    /// <summary>
    /// Validates the structure of CBEFF data without fully deserializing.
    /// </summary>
    /// <param name="data">The binary CBEFF data to validate</param>
    /// <returns>A Result indicating whether the data is valid CBEFF</returns>
    Result<bool> ValidateStructure(ImmutableArray<byte> data);

    /// <summary>
    /// Validates the structure of CBEFF data without fully deserializing.
    /// </summary>
    /// <param name="data">The binary CBEFF data to validate</param>
    /// <returns>A Result indicating whether the data is valid CBEFF</returns>
    Result<bool> ValidateStructure(byte[] data);
}
