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
/// INCITS 378-2004 Ridge Count Data per Section 6.6.2.
/// Type Identification Code: 0x0001 per Table 3.
/// Contains information about the number of fingerprint ridges between pairs of minutiae.
/// "Ridge counts shall not include the ridges represented by either of the associated minutiae."
/// </summary>
public sealed record RidgeCountData : ExtendedDataBlock
{
    /// <summary>
    /// Ridge count extraction method per INCITS 378-2004 Section 6.6.2.1 Table 4.
    /// Indicates how ridge counts were extracted and their organization in the record.
    /// </summary>
    public RidgeCountExtractionMethod ExtractionMethod { get; }

    /// <summary>
    /// Ridge count entries per INCITS 378-2004 Section 6.6.2.2.
    /// Each entry is 3 bytes: index1 (1 byte) + index2 (1 byte) + count (1 byte).
    /// "The ridge count data shall be listed in increasing order of the index numbers."
    /// </summary>
    public ImmutableArray<RidgeCountEntry> RidgeCounts { get; }

    private RidgeCountData(
        RidgeCountExtractionMethod extractionMethod,
        ImmutableArray<RidgeCountEntry> ridgeCounts
    )
        : base(0x0001, CalculateDataLength(ridgeCounts))
    {
        ExtractionMethod = extractionMethod;
        RidgeCounts = ridgeCounts;
    }

    /// <summary>
    /// Creates ridge count data with validation per INCITS 378-2004 Section 6.6.2.
    /// </summary>
    /// <param name="extractionMethod">Method used to extract ridge counts.</param>
    /// <param name="ridgeCounts">Collection of ridge count entries.</param>
    /// <returns>Success with RidgeCountData or Failure with error message.</returns>
    public static Result<RidgeCountData> Create(
        RidgeCountExtractionMethod extractionMethod,
        ImmutableArray<RidgeCountEntry> ridgeCounts
    )
    {
        var errors = new List<string>();

        // Validate extraction method is defined in Table 4
        if (!Enum.IsDefined(extractionMethod))
        {
            errors.Add(
                $"Invalid extraction method: {extractionMethod} (see INCITS 378-2004 Table 4)"
            );
        }

        // Validate specific extraction method constraints per Section 6.6.2.1
        if (extractionMethod == RidgeCountExtractionMethod.FourNeighbor)
        {
            if (ridgeCounts.Length != 4)
            {
                errors.Add("Quadrant method must have exactly 4 entries");
            }
        }
        else if (extractionMethod == RidgeCountExtractionMethod.EightNeighbor)
        {
            if (ridgeCounts.Length != 8)
            {
                errors.Add("Octant method must have exactly 8 entries");
            }
        }

        var computedLength = 4 + 1 + (3 * ridgeCounts.Length);
        if (computedLength < 5 || computedLength > ushort.MaxValue)
        {
            errors.Add("Ridge count data length exceeds maximum");
        }

        return errors.Count == 0
            ? Result.Success(new RidgeCountData(extractionMethod, ridgeCounts))
            : Result.Failure<RidgeCountData>(string.Join("; ", errors));
    }

    /// <summary>
    /// Validates the ridge count data meets INCITS 378-2004 compliance requirements.
    /// </summary>
    public override Result ValidateCompliance()
    {
        // First validate common extended data requirements
        var commonValidation = ValidateCommonRequirements();
        if (commonValidation.IsFailure)
        {
            return commonValidation;
        }

        var errors = new List<string>();

        // Verify Type ID is correct for ridge count data
        if (TypeIdentificationCode != 0x0001)
        {
            errors.Add(
                $"Ridge count data must have Type ID 0x0001, got 0x{TypeIdentificationCode:X4}"
            );
        }

        // Verify data length calculation
        var expectedLength = CalculateDataLength(RidgeCounts);
        if (DataLength != expectedLength)
        {
            errors.Add(
                $"Data length mismatch: header says {DataLength}, calculated {expectedLength}"
            );
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }

    /// <summary>
    /// Calculates data length per INCITS 378-2004 Section 6.6.1.3.
    /// Length = 4 (type + length) + 1 (method) + 3 * count (ridge entries).
    /// </summary>
    private static ushort CalculateDataLength(ImmutableArray<RidgeCountEntry> ridgeCounts) =>
        // 2 bytes for type ID (Section 6.6.1.2)
        // 2 bytes for length field (Section 6.6.1.3)
        // 1 byte for extraction method (Section 6.6.2.1)
        // 3 bytes per ridge count entry (Section 6.6.2.2)
        (ushort)(4 + 1 + (3 * ridgeCounts.Length));

    /// <summary>Checks that ridge-count references identify minutiae present in this view.</summary>
    public Result ValidateWithContext(byte totalMinutiae)
    {
        var errors = new List<string>();

        for (int i = 0; i < RidgeCounts.Length; i++)
        {
            var entry = RidgeCounts[i];
            if (entry.IsNull)
            {
                continue;
            }

            if (
                ExtractionMethod == RidgeCountExtractionMethod.Nonspecific
                || ExtractionMethod == RidgeCountExtractionMethod.Specified
            )
            {
                if (entry.FirstMinutiaIndex == 0 || entry.SecondMinutiaIndex == 0)
                {
                    errors.Add("Minutia indices cannot be 0 for specified ridge counts");
                    continue;
                }
            }

            if (entry.FirstMinutiaIndex > totalMinutiae || entry.SecondMinutiaIndex > totalMinutiae)
            {
                errors.Add("Minutia index exceeds total minutiae count");
            }
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }

    /// <summary>Returns count statistics for non-null entries, or null values when none are present.</summary>
    public (byte? min, byte? max, double? avg) GetStatistics()
    {
        var values = RidgeCounts
            .Where(entry => !entry.IsNull)
            .Select(entry => entry.RidgeCount)
            .ToArray();

        if (values.Length == 0)
        {
            return (null, null, null);
        }

        var min = values.Min();
        var max = values.Max();
        var avg = values.Average(value => (double)value);
        return (min, max, avg);
    }
}
