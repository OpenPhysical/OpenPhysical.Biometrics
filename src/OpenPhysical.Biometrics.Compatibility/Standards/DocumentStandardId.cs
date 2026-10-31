namespace OpenPhysical.Domains.Standards;

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
/// Represents an immutable document standard identifier.
/// </summary>
/// <remarks>
/// Document standards define the requirements for biometric images used in
/// various identity documents like PIV cards, passports, driver's licenses, etc.
/// </remarks>
public sealed record DocumentStandardId
{
    /// <summary>
    /// Gets the string value of the standard ID.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the DocumentStandardId record.
    /// </summary>
    public DocumentStandardId(string value) =>
        Value = value?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(value));

    /// <summary>
    /// Personal Identity Verification (PIV) standard.
    /// </summary>
    public static DocumentStandardId Piv { get; } = new("PIV");

    /// <summary>
    /// Transportation Worker Identification Credential (TWIC) standard.
    /// </summary>
    public static DocumentStandardId Twic { get; } = new("TWIC");

    /// <summary>
    /// International Civil Aviation Organization (ICAO) passport standard.
    /// </summary>
    public static DocumentStandardId Icao { get; } = new("ICAO");

    /// <summary>
    /// Common Access Card (CAC) military ID standard.
    /// </summary>
    public static DocumentStandardId Cac { get; } = new("CAC");

    /// <summary>
    /// Real ID driver's license standard.
    /// </summary>
    public static DocumentStandardId RealId { get; } = new("REALID");

    /// <summary>
    /// Generic passport photo standard.
    /// </summary>
    public static DocumentStandardId Passport { get; } = new("PASSPORT");

    /// <summary>
    /// Professional headshot standard.
    /// </summary>
    public static DocumentStandardId Headshot { get; } = new("HEADSHOT");

    /// <summary>
    /// Creates a custom document standard ID with validation.
    /// </summary>
    /// <param name="value">The standard identifier.</param>
    /// <returns>A Result containing the DocumentStandardId or an error.</returns>
    public static Result<DocumentStandardId> Create(string value) =>
        Result
            .Success()
            .Ensure(() => !string.IsNullOrWhiteSpace(value), "Standard ID cannot be null or empty")
            .Ensure(() => value.Length <= 20, "Standard ID too long (max 20 characters)")
            .Ensure(
                () => value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'),
                "Standard ID can only contain letters, digits, hyphens, and underscores"
            )
            .Map(() => new DocumentStandardId(value));

    /// <summary>
    /// Returns the string representation of this standard ID.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a DocumentStandardId to its string value.
    /// </summary>
    public static implicit operator string(DocumentStandardId id) => id?.Value ?? string.Empty;
}
