namespace OpenPhysical.Biometrics.Core.Cbeff.Formats;

using CSharpFunctionalExtensions;
using System.Collections.Immutable;

/// <summary>
/// Generic interface for format-specific handlers that parse and extract data from biometric formats.
/// This separates format parsing (INCITS 385, INCITS 378) from modality handling (face, fingerprint).
/// </summary>
/// <typeparam name="TDomainRecord">The domain model type representing the parsed biometric record.</typeparam>
public interface IBiometricFormatHandler<TDomainRecord>
    where TDomainRecord : class
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
    /// Parses biometric data from raw bytes into the typed domain model.
    /// </summary>
    /// <param name="data">The raw biometric data block.</param>
    /// <returns>A parsed domain record or error.</returns>
    Result<TDomainRecord> ParseBiometricData(ImmutableArray<byte> data);
    
    /// <summary>
    /// Serializes a domain record back to raw bytes.
    /// </summary>
    /// <param name="record">The domain record to serialize.</param>
    /// <returns>The serialized bytes or error.</returns>
    Result<ImmutableArray<byte>> SerializeBiometricData(TDomainRecord record);
    
    /// <summary>
    /// Validates that the biometric data conforms to the format specification.
    /// </summary>
    /// <param name="data">The raw biometric data block.</param>
    /// <returns>Success if valid, failure with validation errors.</returns>
    Result ValidateBiometricData(ImmutableArray<byte> data);
}