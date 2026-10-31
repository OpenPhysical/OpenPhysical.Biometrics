namespace OpenPhysical.Domains.Cbeff.Formats.Incits378;

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
/// Ridge count entry per INCITS 378-2004 Section 6.6.2.2.
/// Represents the number of fingerprint ridges between a pair of minutiae.
/// Each entry is 3 bytes: index1 (1 byte) + index2 (1 byte) + count (1 byte).
/// </summary>
public sealed record RidgeCountEntry
{
    /// <summary>
    /// Index of the first minutia (1-based) per Section 6.6.2.2.
    /// References a minutia in the corresponding minutiae area by its position.
    /// Valid range: 1 to number of minutiae in the finger view.
    /// </summary>
    public byte FirstMinutiaIndex { get; }

    /// <summary>
    /// Index of the second minutia (1-based) per Section 6.6.2.2.
    /// References a minutia in the corresponding minutiae area by its position.
    /// Valid range: 1 to number of minutiae in the finger view.
    /// </summary>
    public byte SecondMinutiaIndex { get; }

    /// <summary>
    /// Count of ridges intersected by a direct line between these two minutiae.
    /// Per Section 6.6.2: "Ridge counts shall not include the ridges represented
    /// by either of the associated minutiae."
    /// Valid range: 0-255 ridges.
    /// </summary>
    public byte RidgeCount { get; }

    private RidgeCountEntry(byte firstMinutiaIndex, byte secondMinutiaIndex, byte ridgeCount)
    {
        FirstMinutiaIndex = firstMinutiaIndex;
        SecondMinutiaIndex = secondMinutiaIndex;
        RidgeCount = ridgeCount;
    }

    /// <summary>
    /// Creates a new RidgeCountEntry with validation per INCITS 378-2004.
    /// </summary>
    /// <param name="firstMinutiaIndex">Index of first minutia (1-based).</param>
    /// <param name="secondMinutiaIndex">Index of second minutia (1-based).</param>
    /// <param name="ridgeCount">Number of ridges between minutiae.</param>
    /// <param name="totalMinutiaeCount">Total number of minutiae in the finger view for validation.</param>
    /// <returns>Success with RidgeCountEntry or Failure with error message.</returns>
    public static Result<RidgeCountEntry> Create(
        byte firstMinutiaIndex,
        byte secondMinutiaIndex,
        byte ridgeCount,
        byte totalMinutiaeCount
    )
    {
        var errors = new List<string>();

        // Validate first minutia index
        if (firstMinutiaIndex == 0)
        {
            errors.Add("First minutia index cannot be 0 (indices are 1-based per INCITS 378-2004)");
        }
        else if (firstMinutiaIndex > totalMinutiaeCount)
        {
            errors.Add(
                $"First minutia index {firstMinutiaIndex} exceeds total minutiae count {totalMinutiaeCount}"
            );
        }

        // Validate second minutia index
        if (secondMinutiaIndex == 0)
        {
            errors.Add(
                "Second minutia index cannot be 0 (indices are 1-based per INCITS 378-2004)"
            );
        }
        else if (secondMinutiaIndex > totalMinutiaeCount)
        {
            errors.Add(
                $"Second minutia index {secondMinutiaIndex} exceeds total minutiae count {totalMinutiaeCount}"
            );
        }

        // Validate indices are different (can't have ridge count to same minutia)
        if (firstMinutiaIndex == secondMinutiaIndex)
        {
            errors.Add(
                $"Ridge count cannot be between the same minutia (both indices are {firstMinutiaIndex})"
            );
        }

        return errors.Count == 0
            ? Result.Success(new RidgeCountEntry(firstMinutiaIndex, secondMinutiaIndex, ridgeCount))
            : Result.Failure<RidgeCountEntry>(string.Join("; ", errors));
    }

    /// <summary>
    /// Creates a "null" ridge count entry for quadrant/octant methods.
    /// Per Section 6.6.2.1: "if a given quadrant or octant has no neighboring minutiae in it,
    /// a ridge count field shall be recorded with both the minutia index and the ridge count
    /// fields set to zero."
    /// </summary>
    /// <returns>A null ridge count entry with all fields set to zero.</returns>
    public static RidgeCountEntry CreateNull() =>
        // Special case for empty quadrants/octants - bypass validation
        new(0, 0, 0);

    /// <summary>
    /// Indicates if this is a null entry (used for empty quadrants/octants).
    /// </summary>
    public bool IsNull => FirstMinutiaIndex == 0 && SecondMinutiaIndex == 0 && RidgeCount == 0;

    /// <summary>
    /// Creates a ridge count entry for parsing without validation.
    /// Used during parsing when total minutiae count is not yet known.
    /// Validation should be performed later when complete context is available.
    /// </summary>
    /// <param name="firstMinutiaIndex">Index of first minutia.</param>
    /// <param name="secondMinutiaIndex">Index of second minutia.</param>
    /// <param name="ridgeCount">Number of ridges between minutiae.</param>
    /// <returns>A ridge count entry without validation.</returns>
    public static RidgeCountEntry CreateForParsing(
        byte firstMinutiaIndex,
        byte secondMinutiaIndex,
        byte ridgeCount
    ) =>
        // Create without validation for parsing scenarios
        new(firstMinutiaIndex, secondMinutiaIndex, ridgeCount);
}
