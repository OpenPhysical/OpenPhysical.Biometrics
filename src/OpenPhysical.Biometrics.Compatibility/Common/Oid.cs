namespace OpenPhysical.Domains.Common;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Asn1;

/// <summary>
/// Represents an immutable Object Identifier (OID) value object.
/// </summary>
/// <remarks>
/// OIDs are hierarchical identifiers used in various standards including
/// X.509 certificates, LDAP, SNMP, and many cryptographic standards.
/// </remarks>
public sealed partial record Oid
{
    private static readonly Regex OidPattern = OidPatternRegex();

    /// <summary>
    /// Gets the OID string value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the OID components as an array of integers.
    /// </summary>
    public IReadOnlyList<int> Components { get; }

    /// <summary>
    /// Initializes a new instance of the Oid record.
    /// </summary>
    private Oid(string value, int[] components)
    {
        Value = value;
        Components = components;
    }

    /// <summary>
    /// Creates an OID from a string value.
    /// </summary>
    /// <param name="value">The OID string (e.g., "1.2.840.113549.1.1.1").</param>
    /// <returns>A Result containing the OID or an error.</returns>
    public static Result<Oid> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<Oid>("OID value cannot be null or empty");

        if (!OidPattern.IsMatch(value))
            return Result.Failure<Oid>("OID must consist of dot-separated numbers");

        var parts = value.Split('.');
        var components = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var component) || component < 0)
                return Result.Failure<Oid>($"Invalid OID component: {parts[i]}");

            components[i] = component;
        }

        // Validate OID structure rules
        if (components.Length < 2)
            return Result.Failure<Oid>("OID must have at least 2 components");

        if (components[0] > 2)
            return Result.Failure<Oid>("First OID component must be 0, 1, or 2");

        if (components[0] < 2 && components[1] > 39)
            return Result.Failure<Oid>(
                "When first component is 0 or 1, second component must be <= 39"
            );

        return Result.Success(new Oid(value, components));
    }

    /// <summary>
    /// Creates an OID from a DER object identifier.
    /// </summary>
    /// <param name="derOid">The DER object identifier.</param>
    /// <returns>A Result containing the OID or an error.</returns>
    public static Result<Oid> FromDerObjectIdentifier(DerObjectIdentifier derOid)
    {
        if (derOid == null)
            return Result.Failure<Oid>("DER object identifier cannot be null");

        return Create(derOid.Id);
    }

    /// <summary>
    /// Converts this OID to a DER object identifier.
    /// </summary>
    /// <returns>The DER object identifier.</returns>
    public DerObjectIdentifier ToDerObjectIdentifier() => new(Value);

    /// <summary>
    /// Determines if this OID is a child of another OID.
    /// </summary>
    /// <param name="parent">The potential parent OID.</param>
    /// <returns>True if this OID is a child of the parent OID.</returns>
    public bool IsChildOf(Oid parent)
    {
        if (parent == null || Components.Count <= parent.Components.Count)
            return false;

        for (int i = 0; i < parent.Components.Count; i++)
        {
            if (Components[i] != parent.Components[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the parent OID of this OID.
    /// </summary>
    /// <returns>A Maybe containing the parent OID, or None if this is a root OID.</returns>
    public Maybe<Oid> GetParent()
    {
        if (Components.Count <= 2)
            return Maybe<Oid>.None;

        var parentComponents = Components.Take(Components.Count - 1);
        var parentValue = string.Join(".", parentComponents);

        var result = Create(parentValue);
        return result.IsSuccess ? Maybe<Oid>.From(result.Value) : Maybe<Oid>.None;
    }

    /// <summary>
    /// Returns the string representation of this OID.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts an OID to its string value.
    /// </summary>
    public static implicit operator string(Oid oid) => oid?.Value ?? string.Empty;

    [GeneratedRegex("^[0-9]+(\\.[0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex OidPatternRegex();
}
