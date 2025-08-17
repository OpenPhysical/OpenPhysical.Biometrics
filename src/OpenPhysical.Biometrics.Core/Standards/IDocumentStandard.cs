namespace OpenPhysical.Biometrics.Core.Standards;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Standards;
using OpenPhysical.Biometrics.Core.Domain.Transformations;

/// <summary>
/// Defines the interface for document-specific biometric image standards.
/// </summary>
/// <remarks>
/// Each document standard (PIV, TWIC, ICAO, etc.) implements this interface
/// to provide its specific requirements and processing logic.
/// </remarks>
public interface IDocumentStandard
{
    /// <summary>
    /// Gets the unique identifier for this standard.
    /// </summary>
    DocumentStandardId Id { get; }

    /// <summary>
    /// Gets the human-readable name of the standard.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of the standard and its use cases.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the required output dimensions for images.
    /// </summary>
    OpenPhysical.Domains.Biometrics.ImageDimensions RequiredDimensions { get; }

    /// <summary>
    /// Gets the biometric modalities supported by this standard.
    /// </summary>
    IReadOnlyList<BiometricModality> SupportedModalities { get; }

    /// <summary>
    /// Gets all compliance requirements for this standard.
    /// </summary>
    IReadOnlyList<ComplianceRequirement> Requirements { get; }

    /// <summary>
    /// Gets the default processing options for this standard.
    /// </summary>
    ProcessingOptions DefaultProcessingOptions { get; }

    /// <summary>
    /// Creates a processor configured for this standard.
    /// </summary>
    /// <returns>An image processor configured for this standard's requirements.</returns>
    IImageProcessor CreateProcessor();

    /// <summary>
    /// Validates that an image meets this standard's requirements.
    /// </summary>
    /// <param name="imageData">The image data to validate.</param>
    /// <param name="metadata">Optional metadata about the image.</param>
    /// <returns>A Result indicating success or containing validation errors.</returns>
    Task<Result<ComplianceValidation>> ValidateAsync(
        byte[] imageData, 
        Maybe<ImageMetadata> metadata = default);

    /// <summary>
    /// Gets the file size constraints for this standard.
    /// </summary>
    (int minBytes, int maxBytes) FileSizeConstraints { get; }

    /// <summary>
    /// Gets the supported output formats for this standard.
    /// </summary>
    IReadOnlyList<string> SupportedOutputFormats { get; }
}

/// <summary>
/// Represents the result of compliance validation.
/// </summary>
public sealed record ComplianceValidation(
    bool IsCompliant,
    IReadOnlyList<ComplianceViolation> Violations,
    IReadOnlyDictionary<string, float> Measurements,
    TimeSpan ValidationTime);

/// <summary>
/// Represents a specific compliance violation.
/// </summary>
public sealed record ComplianceViolation(
    ComplianceRequirement Requirement,
    float ActualValue,
    string Description);

/// <summary>
/// Metadata about an image being processed.
/// </summary>
public sealed record ImageMetadata(
    Maybe<string> SourcePath,
    Maybe<DateTime> CaptureDate,
    Maybe<string> DeviceInfo,
    IReadOnlyDictionary<string, object> AdditionalData);

/// <summary>
/// Interface for processors that transform images according to a standard.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Processes an image according to the configured standard.
    /// </summary>
    /// <param name="imageData">The input image data.</param>
    /// <param name="options">Processing options.</param>
    /// <returns>The processed image data.</returns>
    Task<Result<ProcessedImage>> ProcessAsync(
        byte[] imageData, 
        ProcessingOptions options);
}

/// <summary>
/// Represents a processed image with metadata.
/// </summary>
public sealed record ProcessedImage(
    byte[] ImageData,
    OpenPhysical.Domains.Biometrics.ImageDimensions Dimensions,
    IReadOnlyDictionary<string, object> ProcessingMetadata);