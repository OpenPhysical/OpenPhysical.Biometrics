namespace OpenPhysical.Biometrics.Core.Cbeff.Formats;

using CSharpFunctionalExtensions;
using System.Collections.Immutable;

/// <summary>
/// Interface for format-specific handlers that parse and serialize biometric data within CBEFF structures.
/// </summary>
public interface ICbeffFormat
{
    /// <summary>
    /// Gets the format name for display purposes.
    /// </summary>
    string FormatName { get; }
    
    /// <summary>
    /// Gets the patron format identifier this handler supports.
    /// </summary>
    byte PatronFormat { get; }
    
    /// <summary>
    /// Gets the BDB format owner this handler supports.
    /// </summary>
    ushort BdbFormatOwner { get; }
    
    /// <summary>
    /// Gets the BDB format type this handler supports.
    /// </summary>
    ushort BdbFormatType { get; }
    
    /// <summary>
    /// Parses biometric data from the raw bytes.
    /// </summary>
    /// <param name="data">The raw biometric data block.</param>
    /// <returns>A parsed biometric record or error.</returns>
    Result<BiometricRecord> ParseBiometricData(ImmutableArray<byte> data);
    
    /// <summary>
    /// Serializes a biometric record to raw bytes.
    /// </summary>
    /// <param name="record">The biometric record to serialize.</param>
    /// <returns>The serialized bytes or error.</returns>
    Result<ImmutableArray<byte>> SerializeBiometricData(BiometricRecord record);
    
    /// <summary>
    /// Validates that the biometric data conforms to the format specification.
    /// </summary>
    /// <param name="data">The raw biometric data block.</param>
    /// <returns>Success if valid, failure with validation errors.</returns>
    Result ValidateBiometricData(ImmutableArray<byte> data);
}