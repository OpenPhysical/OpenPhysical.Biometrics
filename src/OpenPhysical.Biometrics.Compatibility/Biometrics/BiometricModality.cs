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
/// Represents an immutable biometric modality type.
/// </summary>
/// <remarks>
/// Biometric modalities are the different types of biometric data that can be captured
/// and processed, such as facial, fingerprint, iris, voice, etc.
/// </remarks>
public sealed record BiometricModality
{
    /// <summary>
    /// Gets the string value of the modality.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the BiometricModality record.
    /// </summary>
    private BiometricModality(string value) => Value = value;

    /// <summary>
    /// Facial biometric modality.
    /// </summary>
    public static BiometricModality Face { get; } = new("FACE");

    /// <summary>
    /// Fingerprint biometric modality.
    /// </summary>
    public static BiometricModality Fingerprint { get; } = new("FINGERPRINT");

    /// <summary>
    /// Iris biometric modality.
    /// </summary>
    public static BiometricModality Iris { get; } = new("IRIS");

    /// <summary>
    /// Voice biometric modality.
    /// </summary>
    public static BiometricModality Voice { get; } = new("VOICE");

    /// <summary>
    /// Palm print biometric modality.
    /// </summary>
    public static BiometricModality PalmPrint { get; } = new("PALMPRINT");

    /// <summary>
    /// Vein pattern biometric modality.
    /// </summary>
    public static BiometricModality Vein { get; } = new("VEIN");

    /// <summary>
    /// DNA biometric modality.
    /// </summary>
    public static BiometricModality Dna { get; } = new("DNA");

    /// <summary>
    /// Signature biometric modality.
    /// </summary>
    public static BiometricModality Signature { get; } = new("SIGNATURE");

    /// <summary>
    /// Gets all standard biometric modalities.
    /// </summary>
    public static IReadOnlyList<BiometricModality> All { get; } =
        new[] { Face, Fingerprint, Iris, Voice, PalmPrint, Vein, Dna, Signature };

    /// <summary>
    /// Creates a custom biometric modality.
    /// </summary>
    /// <param name="value">The modality identifier.</param>
    /// <returns>A Result containing the BiometricModality or an error.</returns>
    public static Result<BiometricModality> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<BiometricModality>("Biometric modality cannot be null or empty");

        if (value.Length > 50)
            return Result.Failure<BiometricModality>(
                "Biometric modality value too long (max 50 characters)"
            );

        return Result.Success(new BiometricModality(value.ToUpperInvariant()));
    }

    /// <summary>
    /// Tries to parse a standard biometric modality from a string.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <returns>A Maybe containing the BiometricModality if found.</returns>
    public static Maybe<BiometricModality> FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Maybe<BiometricModality>.None;

        var normalized = value.ToUpperInvariant();

        foreach (var modality in All)
        {
            if (modality.Value == normalized)
                return Maybe<BiometricModality>.From(modality);
        }

        return Maybe<BiometricModality>.None;
    }

    /// <summary>
    /// Returns the string representation of this biometric modality.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a BiometricModality to its string value.
    /// </summary>
    public static implicit operator string(BiometricModality modality) =>
        modality?.Value ?? string.Empty;
}
