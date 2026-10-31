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
/// INCITS 378 complete fingerprint minutiae record.
/// Contains the general header and all finger view records for a fingerprint template.
/// </summary>
public sealed record FingerprintRecord
{
    /// <summary>
    /// General record header containing metadata.
    /// </summary>
    public GeneralRecordHeader Header { get; }

    /// <summary>
    /// Collection of finger view records containing minutiae data.
    /// </summary>
    public ImmutableArray<FingerViewRecord> ViewRecords { get; }

    /// <summary>
    /// Total number of minutiae across all views.
    /// </summary>
    public int TotalMinutiae => ViewRecords.Sum(r => r.NumberOfMinutiae);

    /// <summary>
    /// Indicates if this is an empty template (no minutiae).
    /// Used for individuals who cannot be fingerprinted per SP 800-76-2 Section 4.4.3.
    /// </summary>
    public bool IsEmptyTemplate => TotalMinutiae == 0;

    /// <summary>
    /// Indicates if this record contains extended data blocks.
    /// Per INCITS 378-2004 page vii: Records with extended data use CBEFF Format Type 0x0202.
    /// </summary>
    public bool HasExtendedData => ViewRecords.Any(v => v.ExtendedData.Length > 0);

    /// <summary>Combines a parsed general header and its immutable finger views.</summary>
    public FingerprintRecord(
        GeneralRecordHeader header,
        ImmutableArray<FingerViewRecord> viewRecords
    )
    {
        Header = header;
        ViewRecords = viewRecords;
    }

    /// <summary>
    /// Creates a new FingerprintRecord with validation.
    /// </summary>
    public static Result<FingerprintRecord> Create(
        GeneralRecordHeader header,
        ImmutableArray<FingerViewRecord> viewRecords
    )
    {
        // Validate view record count matches header
        if (viewRecords.Length != header.NumberOfFingerRecords)
            return Result.Failure<FingerprintRecord>(
                $"View record count mismatch: header says {header.NumberOfFingerRecords}, but {viewRecords.Length} provided"
            );

        // Validate we don't exceed reasonable limits
        if (viewRecords.Length > 10)
            return Result.Failure<FingerprintRecord>(
                $"Too many view records: {viewRecords.Length} (maximum 10)"
            );

        // Validate total record size matches header
        var calculatedSize = CalculateRecordSize(header, viewRecords);
        if (calculatedSize != header.RecordLength)
            return Result.Failure<FingerprintRecord>(
                $"Record size mismatch: header says {header.RecordLength} bytes, but calculated {calculatedSize} bytes"
            );

        return Result.Success(new FingerprintRecord(header, viewRecords));
    }

    /// <summary>
    /// Validates the complete record for format compliance.
    /// </summary>
    public Result ValidateFormat()
    {
        var errors = new List<string>();

        // Validate header
        var headerValidation = Header.ValidateCompliance();
        if (headerValidation.IsFailure)
            errors.Add($"Header validation failed: {headerValidation.Error}");

        // Validate each view record
        for (int i = 0; i < ViewRecords.Length; i++)
        {
            var viewValidation = ViewRecords[i].ValidatePivCompliance();
            if (viewValidation.IsFailure)
                errors.Add($"View record {i} validation failed: {viewValidation.Error}");
        }

        // Check for duplicate finger positions
        var positions = ViewRecords.GroupBy(r => r.FingerPosition).Where(g => g.Count() > 1);
        foreach (var group in positions)
        {
            if (group.Count() > 1)
            {
                var viewNumbers = group.Select(r => r.ViewNumber).ToArray();
                if (viewNumbers.Distinct().Count() != viewNumbers.Length)
                    errors.Add(
                        $"Duplicate view numbers for finger position {group.Key}: {string.Join(", ", viewNumbers)}"
                    );
            }
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }

    /// <summary>
    /// Validates the record specifically for PIV compliance per SP 800-76-2.
    /// </summary>
    public Result ValidatePivCompliance()
    {
        var errors = new List<string>();

        // PIV typically uses 2 fingers: right index (7) and left index (2)
        if (ViewRecords.Length != 2)
            errors.Add($"PIV templates typically have 2 finger records, got {ViewRecords.Length}");

        // Check for expected finger positions
        var positions = ViewRecords.Select(r => r.FingerPosition).ToArray();
        var expectedPositions = new byte[] { 2, 7 }; // Left index, right index
        var missingPositions = expectedPositions.Except(positions).ToArray();
        var extraPositions = positions.Except(expectedPositions).ToArray();

        if (missingPositions.Length > 0)
            errors.Add(
                $"Missing expected PIV finger positions: {string.Join(", ", missingPositions)}"
            );

        if (extraPositions.Length > 0)
            errors.Add($"Unexpected finger positions for PIV: {string.Join(", ", extraPositions)}");

        // Validate individual records
        var baseValidation = ValidateFormat();
        if (baseValidation.IsFailure)
            errors.Add(baseValidation.Error);

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }

    /// <summary>
    /// Gets all minutiae from all views.
    /// </summary>
    public ImmutableArray<MinutiaPoint> GetAllMinutiae() =>
        ViewRecords.SelectMany(r => r.Minutiae).ToImmutableArray();

    /// <summary>
    /// Gets minutiae for a specific finger position.
    /// </summary>
    public ImmutableArray<MinutiaPoint> GetMinutiaeForFinger(byte fingerPosition) =>
        ViewRecords
            .Where(r => r.FingerPosition == fingerPosition)
            .SelectMany(r => r.Minutiae)
            .ToImmutableArray();

    /// <summary>
    /// Gets the quality summary for the template.
    /// </summary>
    public (double averageQuality, int totalMinutiae, bool hasUnusableFingers) GetQualitySummary()
    {
        var allMinutiae = GetAllMinutiae();
        var totalMinutiae = allMinutiae.Length;

        if (totalMinutiae == 0)
        {
            var unusableFingers = ViewRecords.Any(r => r.FingerQuality >= 254);
            return (0.0, 0, unusableFingers);
        }

        var qualityMinutiae = allMinutiae.Where(m => m.Quality > 0).ToList();
        var averageQuality =
            qualityMinutiae.Count > 0 ? qualityMinutiae.Average(m => m.Quality) : 0.0;
        var hasUnusableFingers = ViewRecords.Any(r => r.FingerQuality >= 254);

        return (averageQuality, totalMinutiae, hasUnusableFingers);
    }

    /// <summary>
    /// Calculates the expected record size in bytes.
    /// Updated to include extended data per INCITS 378-2004 Section 6.6.
    /// </summary>
    private static uint CalculateRecordSize(
        GeneralRecordHeader header,
        ImmutableArray<FingerViewRecord> viewRecords
    )
    {
        // General record header: 26 bytes per Section 6.4
        uint size = 26;

        // Each finger view record per Section 6.3:
        // - 4-byte header (not 6!) per Section 6.5.1
        // - 6 bytes per minutia per Section 6.5.2
        // - Extended data area per Section 6.6
        foreach (var view in viewRecords)
        {
            // Use the view's own calculation method which includes extended data
            size += (uint)view.CalculateRecordSize();
        }

        return size;
    }
}
