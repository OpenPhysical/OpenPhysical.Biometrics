namespace OpenPhysical.Biometrics.Core.Cbeff.Formats;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Domain;

/// <summary>
/// Registry for managing generic biometric format handlers that support self-registration.
/// </summary>
public interface IBiometricFormatRegistry
{
    /// <summary>
    /// Registers a format handler using its attribute metadata.
    /// </summary>
    /// <typeparam name="TDomainRecord">The domain model type.</typeparam>
    /// <param name="formatHandler">The format handler to register.</param>
    /// <returns>Success or failure with error message.</returns>
    Result RegisterFormatHandler<TDomainRecord>(IBiometricFormatHandler<TDomainRecord> formatHandler)
        where TDomainRecord : class;
        
    /// <summary>
    /// Registers a modality handler for a specific domain type.
    /// </summary>
    /// <typeparam name="TDomainRecord">The domain model type.</typeparam>
    /// <param name="modalityHandler">The modality handler to register.</param>
    /// <returns>Success or failure with error message.</returns>
    Result RegisterModalityHandler<TDomainRecord>(IBiometricModalityHandler<TDomainRecord, ExtractedBiometricData> modalityHandler)
        where TDomainRecord : class;
        
    /// <summary>
    /// Gets a format handler for the specified format identifiers.
    /// </summary>
    /// <param name="patronFormat">The patron format identifier.</param>
    /// <param name="bdbFormatOwner">The BDB format owner.</param>
    /// <param name="bdbFormatType">The BDB format type.</param>
    /// <returns>The format handler if found, otherwise failure.</returns>
    Maybe<object> GetFormatHandler(byte patronFormat, ushort bdbFormatOwner, ushort bdbFormatType);
    
    /// <summary>
    /// Gets a modality handler for the specified domain type.
    /// </summary>
    /// <typeparam name="TDomainRecord">The domain model type.</typeparam>
    /// <returns>The modality handler if found, otherwise failure.</returns>
    Maybe<IBiometricModalityHandler<TDomainRecord, ExtractedBiometricData>> GetModalityHandler<TDomainRecord>()
        where TDomainRecord : class;
        
    /// <summary>
    /// Processes biometric data through the complete pipeline (parse → extract).
    /// </summary>
    /// <param name="patronFormat">The patron format identifier.</param>
    /// <param name="bdbFormatOwner">The BDB format owner.</param>
    /// <param name="bdbFormatType">The BDB format type.</param>
    /// <param name="data">The raw biometric data.</param>
    /// <returns>Complete biometric extraction result or error.</returns>
    Result<BiometricExtraction> ProcessBiometricData(
        byte patronFormat,
        ushort bdbFormatOwner,
        ushort bdbFormatType,
        System.Collections.Immutable.ImmutableArray<byte> data);
        
    /// <summary>
    /// Gets information about all registered format handlers.
    /// </summary>
    /// <returns>List of registered format information.</returns>
    System.Collections.Immutable.ImmutableList<BiometricFormatInfo> GetRegisteredFormats();
}