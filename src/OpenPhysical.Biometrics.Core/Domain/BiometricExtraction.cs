namespace OpenPhysical.Biometrics.Core.Domain;

using System.Collections.Immutable;

/// <summary>
/// Represents the complete result of biometric extraction including data and analysis.
/// </summary>
public sealed record BiometricExtraction
{
    /// <summary>
    /// Gets the extracted biometric data.
    /// </summary>
    public ExtractedBiometricData Data { get; }

    /// <summary>
    /// Gets the analysis results from the modality handler.
    /// </summary>
    public string AnalysisResults { get; }

    /// <summary>
    /// Gets the validation results from format and modality validation.
    /// </summary>
    public ImmutableList<string> ValidationResults { get; }

    /// <summary>
    /// Gets whether the extraction was completely successful.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the source format information.
    /// </summary>
    public BiometricFormatInfo SourceFormat { get; }

    /// <summary>
    /// Initializes a new instance of the BiometricExtraction class.
    /// </summary>
    public BiometricExtraction(
        ExtractedBiometricData data,
        string analysisResults,
        ImmutableList<string> validationResults,
        bool isValid,
        BiometricFormatInfo sourceFormat
    )
    {
        Data = data;
        AnalysisResults = analysisResults;
        ValidationResults = validationResults;
        IsValid = isValid;
        SourceFormat = sourceFormat;
    }
}

/// <summary>
/// Information about the source biometric format.
/// </summary>
public sealed record BiometricFormatInfo
{
    /// <summary>
    /// Gets the format name.
    /// </summary>
    public string FormatName { get; }

    /// <summary>
    /// Gets the patron format identifier.
    /// </summary>
    public byte PatronFormat { get; }

    /// <summary>
    /// Gets the BDB format owner.
    /// </summary>
    public ushort BdbFormatOwner { get; }

    /// <summary>
    /// Gets the BDB format type.
    /// </summary>
    public ushort BdbFormatType { get; }

    /// <summary>
    /// Initializes a new instance of the BiometricFormatInfo class.
    /// </summary>
    public BiometricFormatInfo(
        string formatName,
        byte patronFormat,
        ushort bdbFormatOwner,
        ushort bdbFormatType
    )
    {
        FormatName = formatName;
        PatronFormat = patronFormat;
        BdbFormatOwner = bdbFormatOwner;
        BdbFormatType = bdbFormatType;
    }
}
