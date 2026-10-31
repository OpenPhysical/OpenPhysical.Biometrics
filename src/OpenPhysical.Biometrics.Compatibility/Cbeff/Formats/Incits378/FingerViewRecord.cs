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
/// INCITS 378 Finger View Record per Table 7 of INCITS 378-2004.
/// Contains minutiae data for a single fingerprint view.
/// </summary>
public sealed record FingerViewRecord
{
    /// <summary>
    /// Finger position identifier (1 byte).
    /// Values defined in ANSI/NIST-ITL 1-2000.
    /// </summary>
    public byte FingerPosition { get; }

    /// <summary>
    /// View number for this finger position (4 bits, upper bits of view/impression byte).
    /// Starts at 0 for first view. Packed with ImpressionType per INCITS 378-2004 Section 6.5.1.2.
    /// Range: 0-15 (only 4 bits used).
    /// </summary>
    public byte ViewNumber { get; }

    /// <summary>
    /// Impression type (4 bits, lower bits of view/impression byte).
    /// 0 = Live-scan plain, 1 = Live-scan rolled, etc. Packed with ViewNumber per INCITS 378-2004 Section 6.5.1.3.
    /// Range: 0-15 (only 4 bits used).
    /// </summary>
    public byte ImpressionType { get; }

    /// <summary>
    /// Finger quality (1 byte).
    /// 0-100 scale, or 254/255 for unusable.
    /// </summary>
    public byte FingerQuality { get; }

    /// <summary>
    /// Number of minutiae in this view (1 byte).
    /// </summary>
    public byte NumberOfMinutiae { get; }

    // Note: INCITS 378-2004 Section 6.5.1 specifies 4-byte finger view header with NO reserved field

    /// <summary>
    /// Minutiae data for this view.
    /// </summary>
    public ImmutableArray<MinutiaPoint> Minutiae { get; }

    /// <summary>
    /// Extended data blocks for this finger view per INCITS 378-2004 Section 6.6.
    /// "The extended data for each finger view shall immediately follow the standard minutiae data."
    /// May contain ridge count data (0x0001), core/delta data (0x0002), or vendor-specific data.
    /// Empty if no extended data present (Extended Data Block Length = 0x0000).
    /// </summary>
    public ImmutableArray<ExtendedDataBlock> ExtendedData { get; }

    private FingerViewRecord(
        byte fingerPosition,
        byte viewNumber,
        byte impressionType,
        byte fingerQuality,
        byte numberOfMinutiae,
        ImmutableArray<MinutiaPoint> minutiae,
        ImmutableArray<ExtendedDataBlock> extendedData
    )
    {
        FingerPosition = fingerPosition;
        ViewNumber = viewNumber;
        ImpressionType = impressionType;
        FingerQuality = fingerQuality;
        NumberOfMinutiae = numberOfMinutiae;
        Minutiae = minutiae;
        ExtendedData = extendedData;
    }

    /// <summary>
    /// Creates a new FingerViewRecord with validation.
    /// Supports extended data per INCITS 378-2004 Section 6.6.
    /// </summary>
    public static Result<FingerViewRecord> Create(
        byte fingerPosition,
        byte viewNumber,
        byte impressionType,
        byte fingerQuality,
        byte numberOfMinutiae,
        ImmutableArray<MinutiaPoint> minutiae,
        ImmutableArray<ExtendedDataBlock> extendedData = default
    )
    {
        // Validate finger position (ANSI/NIST-ITL 1-2000 Table 8)
        if (fingerPosition == 0 || fingerPosition > 15)
            return Result.Failure<FingerViewRecord>(
                $"Invalid finger position: {fingerPosition} (must be 1-15)"
            );

        // Validate view number (4 bits max)
        if (viewNumber > 15)
            return Result.Failure<FingerViewRecord>(
                $"Invalid view number: {viewNumber} (must be 0-15)"
            );

        // Validate impression type (4 bits max)
        if (impressionType > 15)
            return Result.Failure<FingerViewRecord>(
                $"Invalid impression type: {impressionType} (must be 0-15)"
            );

        // Validate quality
        if (fingerQuality > 100 && fingerQuality != 254 && fingerQuality != 255)
            return Result.Failure<FingerViewRecord>(
                $"Invalid finger quality: {fingerQuality} (must be 0-100, 254, or 255)"
            );

        // Validate minutiae count
        if (minutiae.Length != numberOfMinutiae)
            return Result.Failure<FingerViewRecord>(
                $"Minutiae count mismatch: header says {numberOfMinutiae}, but {minutiae.Length} provided"
            );

        // PIV allows empty templates per SP 800-76-2 Section 4.4.3
        if (numberOfMinutiae > 128)
            return Result.Failure<FingerViewRecord>(
                $"Too many minutiae: {numberOfMinutiae} (maximum 128 per INCITS 378)"
            );

        // Use empty array if extended data is default
        var extendedDataToUse = extendedData.IsDefault
            ? ImmutableArray<ExtendedDataBlock>.Empty
            : extendedData;

        return Result.Success(
            new FingerViewRecord(
                fingerPosition,
                viewNumber,
                impressionType,
                fingerQuality,
                numberOfMinutiae,
                minutiae,
                extendedDataToUse
            )
        );
    }

    /// <summary>
    /// Validates the view record for PIV compliance.
    /// </summary>
    public Result ValidatePivCompliance()
    {
        var errors = new List<string>();

        // Check finger positions (PIV uses specific positions)
        var validPivPositions = new[] { 2, 7 }; // Left index (2), Right index (7)
        if (!validPivPositions.Contains(FingerPosition))
            errors.Add(
                $"Finger position {FingerPosition} not typically used in PIV (expected 2 or 7)"
            );

        // Check impression type (PIV prefers live-scan plain)
        if (ImpressionType != 0)
            errors.Add(
                $"Impression type {ImpressionType} not preferred for PIV (expected 0 = Live-scan plain)"
            );

        // Validate quality for empty templates
        if (NumberOfMinutiae == 0)
        {
            if (FingerQuality != 254 && FingerQuality != 255)
                errors.Add($"Empty template should have quality 254 or 255, got {FingerQuality}");
        }
        else
        {
            // Non-empty templates should have reasonable quality
            if (FingerQuality < 50 && FingerQuality != 0)
                errors.Add(
                    $"Low quality score {FingerQuality} for template with {NumberOfMinutiae} minutiae"
                );
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }

    /// <summary>
    /// Gets the finger position name for display.
    /// </summary>
    public string GetFingerPositionName() =>
        FingerPosition switch
        {
            1 => "Right thumb",
            2 => "Right index",
            3 => "Right middle",
            4 => "Right ring",
            5 => "Right little",
            6 => "Left thumb",
            7 => "Left index",
            8 => "Left middle",
            9 => "Left ring",
            10 => "Left little",
            11 => "Plain right thumb",
            12 => "Plain left thumb",
            13 => "Plain right four fingers",
            14 => "Plain left four fingers",
            15 => "Plain thumbs",
            _ => $"Unknown ({FingerPosition})",
        };

    /// <summary>
    /// Gets the impression type name for display.
    /// </summary>
    public string GetImpressionTypeName() =>
        ImpressionType switch
        {
            0 => "Live-scan plain",
            1 => "Live-scan rolled",
            2 => "Nonlive-scan plain",
            3 => "Nonlive-scan rolled",
            4 => "Latent impression",
            5 => "Latent tracing",
            6 => "Latent photo",
            7 => "Latent lift",
            8 => "Swipe",
            _ => $"Unknown ({ImpressionType})",
        };

    /// <summary>
    /// Calculates the total size of this finger view record in bytes.
    /// Per INCITS 378-2004 Section 6.3: "A Single Finger record... consisting of:
    /// - A fixed-length (4-byte) header
    /// - A series of fixed-length (6-byte) minutia descriptions
    /// - One or more 'extended' data areas"
    /// </summary>
    /// <returns>Total size in bytes including header, minutiae, and extended data.</returns>
    public int CalculateRecordSize()
    {
        // 4 bytes for finger view header per Section 6.5.1
        var size = 4;

        // 6 bytes per minutia per Section 6.5.2
        size += NumberOfMinutiae * 6;

        // 2 bytes for Extended Data Block Length per Section 6.6.1.1
        size += 2;

        // Sum of all extended data segment lengths
        // Each segment's DataLength includes its type ID and length fields
        foreach (var block in ExtendedData)
        {
            size += block.DataLength;
        }

        return size;
    }
}
