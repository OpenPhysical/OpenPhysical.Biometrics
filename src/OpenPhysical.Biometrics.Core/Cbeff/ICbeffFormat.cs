namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Cbeff;

/// <summary>
/// Defines the interface for CBEFF (Common Biometric Exchange Formats Framework) format implementations.
/// </summary>
/// <remarks>
/// Each CBEFF format (PIV patron format, INCITS formats, etc.) implements this interface
/// to provide encoding and decoding capabilities for biometric data.
/// </remarks>
public interface ICbeffFormat
{
    /// <summary>
    /// Gets the biometric format specification for this CBEFF implementation.
    /// </summary>
    BiometricFormat Format { get; }

    /// <summary>
    /// Gets the supported biometric modalities for this format.
    /// </summary>
    IReadOnlyList<BiometricModality> SupportedModalities { get; }

    /// <summary>
    /// Decodes CBEFF-formatted data into its components.
    /// </summary>
    /// <param name="cbeffData">The raw CBEFF data to decode.</param>
    /// <returns>A Result containing the decoded CBEFF payload or an error.</returns>
    Result<CbeffPayload> Decode(byte[] cbeffData);

    /// <summary>
    /// Encodes biometric data into CBEFF format.
    /// </summary>
    /// <param name="biometricData">The biometric data to encode.</param>
    /// <param name="signature">Optional digital signature for the data.</param>
    /// <returns>A Result containing the encoded CBEFF data or an error.</returns>
    Result<byte[]> Encode(OpenPhysical.Domains.Cbeff.BiometricData biometricData, Maybe<CbeffSignature> signature = default);

    /// <summary>
    /// Validates that CBEFF data conforms to this format's specifications.
    /// </summary>
    /// <param name="cbeffData">The CBEFF data to validate.</param>
    /// <returns>A Result indicating success or containing validation errors.</returns>
    Result Validate(byte[] cbeffData);
}


/// <summary>
/// Registry interface for managing available CBEFF formats.
/// </summary>
public interface ICbeffRegistry
{
    /// <summary>
    /// Registers a CBEFF format implementation.
    /// </summary>
    /// <param name="format">The format implementation to register.</param>
    void Register(ICbeffFormat format);

    /// <summary>
    /// Gets a CBEFF format by its format identifier.
    /// </summary>
    /// <param name="format">The biometric format identifier.</param>
    /// <returns>A Maybe containing the format if found.</returns>
    Maybe<ICbeffFormat> GetFormat(BiometricFormat format);

    /// <summary>
    /// Gets a CBEFF format by owner and type identifiers.
    /// </summary>
    /// <param name="formatOwner">The format owner identifier.</param>
    /// <param name="formatType">The format type identifier.</param>
    /// <returns>A Maybe containing the format if found.</returns>
    Maybe<ICbeffFormat> GetFormat(ushort formatOwner, ushort formatType);

    /// <summary>
    /// Gets all registered CBEFF formats.
    /// </summary>
    /// <returns>All registered format implementations.</returns>
    IEnumerable<ICbeffFormat> GetAllFormats();

    /// <summary>
    /// Attempts to detect and decode CBEFF data using registered formats.
    /// </summary>
    /// <param name="data">The data to decode.</param>
    /// <returns>A Result containing the decoded payload and format used, or an error.</returns>
    Result<(CbeffPayload payload, ICbeffFormat format)> AutoDecode(byte[] data);
}