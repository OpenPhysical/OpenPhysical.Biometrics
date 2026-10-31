namespace OpenPhysical.Biometrics.Core.Cbeff.Formats;

using CSharpFunctionalExtensions;

/// <summary>
/// Generic interface for modality-specific handlers that process biometric data for extraction or analysis.
/// This separates modality processing (facial analysis, minutiae extraction) from format parsing.
/// </summary>
/// <typeparam name="TDomainRecord">The domain model type representing the parsed biometric record.</typeparam>
/// <typeparam name="TExtractionResult">The result type for extracted biometric data.</typeparam>
public interface IBiometricModalityHandler<TDomainRecord, TExtractionResult>
    where TDomainRecord : class
    where TExtractionResult : class
{
    /// <summary>
    /// Gets the modality name for display purposes.
    /// </summary>
    string ModalityName { get; }

    /// <summary>
    /// Determines if this handler can process the given domain record type.
    /// </summary>
    /// <param name="record">The domain record to check.</param>
    /// <returns>True if this handler can process the record.</returns>
    bool CanHandle(TDomainRecord record);

    /// <summary>
    /// Extracts biometric data from the domain record for external use.
    /// </summary>
    /// <param name="record">The parsed domain record.</param>
    /// <returns>The extracted biometric data or error.</returns>
    Result<TExtractionResult> ExtractBiometricData(TDomainRecord record);

    /// <summary>
    /// Analyzes the biometric data and returns quality or analysis information.
    /// </summary>
    /// <param name="record">The parsed domain record.</param>
    /// <returns>Analysis results or error.</returns>
    Result<string> AnalyzeBiometricData(TDomainRecord record);

    /// <summary>
    /// Validates the biometric data meets modality-specific requirements.
    /// </summary>
    /// <param name="record">The parsed domain record.</param>
    /// <returns>Success if valid, failure with validation errors.</returns>
    Result ValidateModalityRequirements(TDomainRecord record);
}
