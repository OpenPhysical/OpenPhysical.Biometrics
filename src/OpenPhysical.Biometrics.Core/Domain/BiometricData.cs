namespace OpenPhysical.Biometrics.Core.Domain;

using System.Collections.Immutable;

/// <summary>
/// Represents extracted biometric data from any format.
/// This is the generic result type for biometric extraction operations.
/// </summary>
public abstract record ExtractedBiometricData
{
    /// <summary>
    /// Gets the modality type (face, fingerprint, iris, etc.).
    /// </summary>
    public abstract string ModalityType { get; }

    /// <summary>
    /// Gets the format identifier that produced this data.
    /// </summary>
    public abstract string FormatIdentifier { get; }

    /// <summary>
    /// Gets the raw extracted data (image bytes, minutiae JSON, etc.).
    /// </summary>
    public ImmutableArray<byte> ExtractedData { get; }

    /// <summary>
    /// Gets the suggested file extension for saving this data.
    /// </summary>
    public abstract string SuggestedExtension { get; }

    /// <summary>
    /// Gets additional metadata about the extracted data.
    /// </summary>
    public ImmutableDictionary<string, object> Metadata { get; }

    /// <summary>
    /// Initializes a new instance of the ExtractedBiometricData class.
    /// </summary>
    /// <param name="extractedData">The raw extracted data.</param>
    /// <param name="metadata">Additional metadata.</param>
    protected ExtractedBiometricData(
        ImmutableArray<byte> extractedData,
        ImmutableDictionary<string, object> metadata
    )
    {
        ExtractedData = extractedData;
        Metadata = metadata;
    }
}

/// <summary>
/// Represents extracted facial image data.
/// </summary>
public sealed record FacialImageData : ExtractedBiometricData
{
    /// <inheritdoc />
    public override string ModalityType => "Face";

    /// <inheritdoc />
    public override string FormatIdentifier { get; }

    /// <inheritdoc />
    public override string SuggestedExtension { get; }

    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the image quality score (0-100).
    /// </summary>
    public byte Quality { get; }

    /// <summary>
    /// Initializes a new instance of the FacialImageData class.
    /// </summary>
    public FacialImageData(
        ImmutableArray<byte> imageData,
        string formatIdentifier,
        string extension,
        int width,
        int height,
        byte quality,
        ImmutableDictionary<string, object> metadata
    )
        : base(imageData, metadata)
    {
        FormatIdentifier = formatIdentifier;
        SuggestedExtension = extension;
        Width = width;
        Height = height;
        Quality = quality;
    }
}

/// <summary>
/// Represents extracted fingerprint minutiae data.
/// </summary>
public sealed record FingerprintMinutiaeData : ExtractedBiometricData
{
    /// <inheritdoc />
    public override string ModalityType => "Fingerprint";

    /// <inheritdoc />
    public override string FormatIdentifier { get; }

    /// <inheritdoc />
    public override string SuggestedExtension => ".json";

    /// <summary>
    /// Gets the total number of minutiae across all finger views.
    /// </summary>
    public int TotalMinutiae { get; }

    /// <summary>
    /// Gets the number of finger view records.
    /// </summary>
    public int NumberOfViews { get; }

    /// <summary>
    /// Gets whether this is an empty template (no minutiae).
    /// </summary>
    public bool IsEmptyTemplate { get; }

    /// <summary>
    /// Initializes a new instance of the FingerprintMinutiaeData class.
    /// </summary>
    public FingerprintMinutiaeData(
        ImmutableArray<byte> jsonData,
        string formatIdentifier,
        int totalMinutiae,
        int numberOfViews,
        bool isEmptyTemplate,
        ImmutableDictionary<string, object> metadata
    )
        : base(jsonData, metadata)
    {
        FormatIdentifier = formatIdentifier;
        TotalMinutiae = totalMinutiae;
        NumberOfViews = numberOfViews;
        IsEmptyTemplate = isEmptyTemplate;
    }
}
