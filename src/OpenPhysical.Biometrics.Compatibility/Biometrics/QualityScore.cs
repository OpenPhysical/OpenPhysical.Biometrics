namespace OpenPhysical.Domains.Biometrics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;

/// <summary>
/// Represents an immutable biometric quality score value object.
/// </summary>
/// <remarks>
/// Quality scores are defined in ISO/IEC 19794 and NIST SP 800-76-2:
/// - Valid range: 0-100 (0 = lowest quality, 100 = highest quality)
/// - Special value: -2 (not supported/not computed)
/// - Special values: 254, 255 (failure to enroll)
/// </remarks>
public readonly record struct QualityScore
{
    /// <summary>
    /// Quality assessment not supported or not computed.
    /// </summary>
    public const int NotSupported = -2;

    /// <summary>
    /// Minimum valid quality score.
    /// </summary>
    public const int MinValid = 0;

    /// <summary>
    /// Maximum valid quality score.
    /// </summary>
    public const int MaxValid = 100;

    /// <summary>
    /// Failure to enroll - sample unusable.
    /// </summary>
    public const int FailureToEnroll = 254;

    /// <summary>
    /// Failure to enroll - unspecified reason.
    /// </summary>
    public const int FailureToEnrollUnspecified = 255;

    /// <summary>
    /// Gets the quality score value.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Gets whether this is a valid quality score (0-100).
    /// </summary>
    public bool IsValid => Value >= MinValid && Value <= MaxValid;

    /// <summary>
    /// Gets whether quality assessment is not supported.
    /// </summary>
    public bool IsNotSupported => Value == NotSupported;

    /// <summary>
    /// Gets whether this indicates a failure to enroll.
    /// </summary>
    public bool IsFailureToEnroll =>
        Value == FailureToEnroll || Value == FailureToEnrollUnspecified;

    /// <summary>
    /// Gets whether this is a special value (not a quality measurement).
    /// </summary>
    public bool IsSpecialValue => IsNotSupported || IsFailureToEnroll;

    /// <summary>
    /// Initializes a new instance of the QualityScore struct.
    /// </summary>
    private QualityScore(int value) => Value = value;

    /// <summary>
    /// Creates a QualityScore from an integer value.
    /// </summary>
    /// <param name="value">The quality score value.</param>
    /// <returns>A Result containing the QualityScore or an error.</returns>
    public static Result<QualityScore> Create(int value) =>
        Result
            .Success()
            .Ensure(
                () => IsValidValue(value),
                $"Quality score must be -2, 0-100, 254, or 255. Got: {value}"
            )
            .Map(() => new QualityScore(value));

    /// <summary>
    /// Creates a QualityScore representing "not supported".
    /// </summary>
    public static QualityScore CreateNotSupported() => new(NotSupported);

    /// <summary>
    /// Creates a QualityScore representing failure to enroll.
    /// </summary>
    public static QualityScore CreateFailureToEnroll() => new(FailureToEnroll);

    /// <summary>
    /// Creates a QualityScore from a percentage (0-100).
    /// </summary>
    /// <param name="percentage">The quality percentage.</param>
    /// <returns>A Result containing the QualityScore or an error.</returns>
    public static Result<QualityScore> FromPercentage(int percentage) =>
        Result
            .Success()
            .Ensure(
                () => percentage >= MinValid && percentage <= MaxValid,
                $"Percentage must be between {MinValid} and {MaxValid}"
            )
            .Map(() => new QualityScore(percentage));

    /// <summary>
    /// Determines if a value is valid for a quality score.
    /// </summary>
    private static bool IsValidValue(int value) =>
        value == NotSupported
        || (value >= MinValid && value <= MaxValid)
        || value == FailureToEnroll
        || value == FailureToEnrollUnspecified;

    /// <summary>
    /// Gets a description of the quality level.
    /// </summary>
    public string GetDescription() =>
        Value switch
        {
            NotSupported => "Not supported",
            >= 0 and <= 20 => "Very poor quality",
            > 20 and <= 40 => "Poor quality",
            > 40 and <= 60 => "Fair quality",
            > 60 and <= 80 => "Good quality",
            > 80 and <= 100 => "Excellent quality",
            FailureToEnroll => "Failure to enroll - sample unusable",
            FailureToEnrollUnspecified => "Failure to enroll - unspecified",
            _ => "Unknown",
        };

    /// <summary>
    /// Converts the quality score to a normalized value (0.0 to 1.0).
    /// </summary>
    /// <returns>A Maybe containing the normalized value, or None if not applicable.</returns>
    public Maybe<double> ToNormalized()
    {
        if (IsValid)
        {
            return Maybe<double>.From(Value / 100.0);
        }
        return Maybe<double>.None;
    }

    /// <summary>
    /// Returns the string representation of this quality score.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Implicitly converts a QualityScore to its integer value.
    /// </summary>
    public static implicit operator int(QualityScore score) => score.Value;
}
