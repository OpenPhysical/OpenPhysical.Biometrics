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
/// Represents an immutable compliance requirement for a document standard.
/// </summary>
/// <remarks>
/// Compliance requirements define the acceptable ranges for various quality
/// metrics that biometric images must meet for a specific standard.
/// </remarks>
public sealed record ComplianceRequirement
{
    /// <summary>
    /// Gets the name of the requirement.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the category of the requirement (e.g., "Geometry", "Quality", "Illumination").
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the minimum acceptable value.
    /// </summary>
    public float MinValue { get; }

    /// <summary>
    /// Gets the maximum acceptable value.
    /// </summary>
    public float MaxValue { get; }

    /// <summary>
    /// Gets the unit of measurement (e.g., "pixels", "percent", "degrees").
    /// </summary>
    public string Unit { get; }

    /// <summary>
    /// Gets whether this requirement is mandatory for compliance.
    /// </summary>
    public bool IsMandatory { get; }

    /// <summary>
    /// Gets the description of the requirement.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the ComplianceRequirement record.
    /// </summary>
    private ComplianceRequirement(
        string name,
        string category,
        float minValue,
        float maxValue,
        string unit,
        bool isMandatory,
        string description
    )
    {
        Name = name;
        Category = category;
        MinValue = minValue;
        MaxValue = maxValue;
        Unit = unit;
        IsMandatory = isMandatory;
        Description = description;
    }

    /// <summary>
    /// Creates a new compliance requirement with validation.
    /// </summary>
    /// <param name="name">The requirement name.</param>
    /// <param name="category">The requirement category.</param>
    /// <param name="minValue">The minimum acceptable value.</param>
    /// <param name="maxValue">The maximum acceptable value.</param>
    /// <param name="unit">The unit of measurement.</param>
    /// <param name="isMandatory">Whether the requirement is mandatory.</param>
    /// <param name="description">The requirement description.</param>
    /// <returns>A Result containing the ComplianceRequirement or an error.</returns>
    public static Result<ComplianceRequirement> Create(
        string name,
        string category,
        float minValue,
        float maxValue,
        string unit,
        bool isMandatory = true,
        string description = ""
    ) =>
        Result
            .Success()
            .Ensure(
                () => !string.IsNullOrWhiteSpace(name),
                "Requirement name cannot be null or empty"
            )
            .Ensure(
                () => !string.IsNullOrWhiteSpace(category),
                "Requirement category cannot be null or empty"
            )
            .Ensure(() => !string.IsNullOrWhiteSpace(unit), "Unit cannot be null or empty")
            .Ensure(
                () => minValue <= maxValue,
                "Minimum value cannot be greater than maximum value"
            )
            .Ensure(
                () => !float.IsNaN(minValue) && !float.IsInfinity(minValue),
                "Invalid minimum value"
            )
            .Ensure(
                () => !float.IsNaN(maxValue) && !float.IsInfinity(maxValue),
                "Invalid maximum value"
            )
            .Map(() =>
                new ComplianceRequirement(
                    name,
                    category,
                    minValue,
                    maxValue,
                    unit,
                    isMandatory,
                    description ?? string.Empty
                )
            );

    /// <summary>
    /// Creates a requirement for a value that must be within a range.
    /// </summary>
    /// <param name="name">The requirement name.</param>
    /// <param name="category">The requirement category.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <param name="unit">The unit of measurement.</param>
    /// <returns>A Result containing the ComplianceRequirement or an error.</returns>
    public static Result<ComplianceRequirement> Range(
        string name,
        string category,
        float min,
        float max,
        string unit
    ) =>
        Create(
            name,
            category,
            min,
            max,
            unit,
            true,
            $"{name} must be between {min} and {max} {unit}"
        );

    /// <summary>
    /// Creates a requirement for a value that must be at least a minimum.
    /// </summary>
    /// <param name="name">The requirement name.</param>
    /// <param name="category">The requirement category.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="unit">The unit of measurement.</param>
    /// <returns>A Result containing the ComplianceRequirement or an error.</returns>
    public static Result<ComplianceRequirement> Minimum(
        string name,
        string category,
        float min,
        string unit
    ) =>
        Create(
            name,
            category,
            min,
            float.MaxValue,
            unit,
            true,
            $"{name} must be at least {min} {unit}"
        );

    /// <summary>
    /// Creates a requirement for a value that must not exceed a maximum.
    /// </summary>
    /// <param name="name">The requirement name.</param>
    /// <param name="category">The requirement category.</param>
    /// <param name="max">The maximum value.</param>
    /// <param name="unit">The unit of measurement.</param>
    /// <returns>A Result containing the ComplianceRequirement or an error.</returns>
    public static Result<ComplianceRequirement> Maximum(
        string name,
        string category,
        float max,
        string unit
    ) =>
        Create(
            name,
            category,
            float.MinValue,
            max,
            unit,
            true,
            $"{name} must not exceed {max} {unit}"
        );

    /// <summary>
    /// Checks if a value meets this requirement.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value meets the requirement.</returns>
    public bool IsMet(float value) => value >= MinValue && value <= MaxValue;

    /// <summary>
    /// Gets a description of why a value fails this requirement.
    /// </summary>
    /// <param name="value">The value that failed.</param>
    /// <returns>A description of the failure.</returns>
    public string GetFailureReason(float value)
    {
        if (value < MinValue)
            return $"{Name}: {value:F1} {Unit} is below minimum of {MinValue:F1} {Unit}";

        if (value > MaxValue)
            return $"{Name}: {value:F1} {Unit} exceeds maximum of {MaxValue:F1} {Unit}";

        return string.Empty;
    }

    /// <summary>
    /// Returns the string representation of this requirement.
    /// </summary>
    public override string ToString() => $"{Name}: {MinValue:F1}-{MaxValue:F1} {Unit} ({Category})";
}
