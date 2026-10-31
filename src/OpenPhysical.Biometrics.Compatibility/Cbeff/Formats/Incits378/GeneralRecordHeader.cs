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
/// INCITS 378 General Record Header per Table 6 of INCITS 378-2004.
/// Contains metadata about the fingerprint minutiae template.
/// </summary>
public sealed record GeneralRecordHeader
{
    /// <summary>
    /// Format identifier - "FMR\0" (4 bytes).
    /// </summary>
    public ImmutableArray<byte> FormatIdentifier { get; }

    /// <summary>
    /// Version number - " 20\0" for INCITS 378-2004 (4 bytes).
    /// </summary>
    public ImmutableArray<byte> VersionNumber { get; }

    /// <summary>
    /// Total record length in bytes (2 or 6 bytes, big-endian).
    /// Values 26-65535 use 2-byte format, values 65536+ use 6-byte format.
    /// </summary>
    public uint RecordLength { get; }

    /// <summary>
    /// CBEFF Product Owner identifier (2 bytes, big-endian).
    /// </summary>
    public ushort ProductOwner { get; }

    /// <summary>
    /// CBEFF Product Type identifier (2 bytes, big-endian).
    /// </summary>
    public ushort ProductType { get; }

    /// <summary>
    /// Capture equipment compliance and ID (2 bytes, big-endian).
    /// Bits 15-12: Equipment compliance (4 bits)
    /// Bits 11-0: Equipment ID (12 bits)
    /// </summary>
    public ushort CaptureEquipmentCompliance { get; }

    /// <summary>
    /// Image size in X direction (width) in pixels (2 bytes, big-endian).
    /// </summary>
    public ushort ImageSizeX { get; }

    /// <summary>
    /// Image size in Y direction (height) in pixels (2 bytes, big-endian).
    /// </summary>
    public ushort ImageSizeY { get; }

    /// <summary>
    /// X scan resolution in pixels per cm (2 bytes, big-endian).
    /// </summary>
    public ushort XResolution { get; }

    /// <summary>
    /// Y scan resolution in pixels per cm (2 bytes, big-endian).
    /// </summary>
    public ushort YResolution { get; }

    /// <summary>
    /// Number of finger view records (1 byte).
    /// </summary>
    public byte NumberOfFingerRecords { get; }

    /// <summary>
    /// Reserved field (1 byte).
    /// </summary>
    public byte Reserved { get; }

    private GeneralRecordHeader(
        ImmutableArray<byte> formatIdentifier,
        ImmutableArray<byte> versionNumber,
        uint recordLength,
        ushort productOwner,
        ushort productType,
        ushort captureEquipmentCompliance,
        ushort imageSizeX,
        ushort imageSizeY,
        ushort xResolution,
        ushort yResolution,
        byte numberOfFingerRecords,
        byte reserved
    )
    {
        FormatIdentifier = formatIdentifier;
        VersionNumber = versionNumber;
        RecordLength = recordLength;
        ProductOwner = productOwner;
        ProductType = productType;
        CaptureEquipmentCompliance = captureEquipmentCompliance;
        ImageSizeX = imageSizeX;
        ImageSizeY = imageSizeY;
        XResolution = xResolution;
        YResolution = yResolution;
        NumberOfFingerRecords = numberOfFingerRecords;
        Reserved = reserved;
    }

    /// <summary>
    /// Creates a new GeneralRecordHeader with validation.
    /// </summary>
    public static Result<GeneralRecordHeader> Create(
        ImmutableArray<byte> formatIdentifier,
        ImmutableArray<byte> versionNumber,
        uint recordLength,
        ushort productOwner,
        ushort productType,
        ushort captureEquipmentCompliance,
        ushort imageSizeX,
        ushort imageSizeY,
        ushort xResolution,
        ushort yResolution,
        byte numberOfFingerRecords,
        byte reserved
    )
    {
        // Validate format identifier
        if (formatIdentifier.Length != 4)
            return Result.Failure<GeneralRecordHeader>("Format identifier must be exactly 4 bytes");

        var formatIdString = Encoding.ASCII.GetString(formatIdentifier.ToArray());
        if (formatIdString != "FMR\0")
            return Result.Failure<GeneralRecordHeader>(
                $"Invalid format identifier: expected 'FMR\\0', got '{formatIdString.Replace("\0", "\\0")}'"
            );

        // Validate version number
        if (versionNumber.Length != 4)
            return Result.Failure<GeneralRecordHeader>("Version number must be exactly 4 bytes");

        var versionString = Encoding.ASCII.GetString(versionNumber.ToArray());
        if (versionString != " 20\0")
            return Result.Failure<GeneralRecordHeader>(
                $"Invalid version number: expected ' 20\\0', got '{versionString.Replace("\0", "\\0")}'"
            );

        // Validate record length
        if (recordLength < 26)
            return Result.Failure<GeneralRecordHeader>(
                "Record length must be at least 26 bytes (minimum header size)"
            );

        if (recordLength > 4294967295)
            return Result.Failure<GeneralRecordHeader>(
                "Record length exceeds maximum value (4294967295)"
            );

        // Validate image size values
        if (imageSizeX == 0)
            return Result.Failure<GeneralRecordHeader>("Image size X cannot be zero");

        if (imageSizeY == 0)
            return Result.Failure<GeneralRecordHeader>("Image size Y cannot be zero");

        // Validate resolution values
        if (xResolution == 0)
            return Result.Failure<GeneralRecordHeader>("X resolution cannot be zero");

        if (yResolution == 0)
            return Result.Failure<GeneralRecordHeader>("Y resolution cannot be zero");

        return Result.Success(
            new GeneralRecordHeader(
                formatIdentifier,
                versionNumber,
                recordLength,
                productOwner,
                productType,
                captureEquipmentCompliance,
                imageSizeX,
                imageSizeY,
                xResolution,
                yResolution,
                numberOfFingerRecords,
                reserved
            )
        );
    }

    /// <summary>
    /// Validates the header data for compliance with INCITS 378-2004.
    /// </summary>
    public Result ValidateCompliance()
    {
        var errors = new List<string>();

        // Check NIST PIV requirements per SP 800-76-2
        if (ProductOwner != 0x0001 || ProductType != 0x0001)
            errors.Add(
                $"Expected NIST Product Owner/Type (0x0001/0x0001), got (0x{ProductOwner:X4}/0x{ProductType:X4})"
            );

        // Check equipment compliance (top 4 bits should be 8 for EFTS compliance)
        var complianceBits = (CaptureEquipmentCompliance >> 12) & 0x0F;
        if (complianceBits != 0x8)
            errors.Add(
                $"Expected equipment compliance 0x8 (EFTS Appendix F compliant), got 0x{complianceBits:X}"
            );

        // Check resolution (PIV typically uses 500 DPI = ~197 pixels/cm)
        var expectedResolution = (ushort)(500 / 2.54); // Convert 500 DPI to pixels per cm
        if (Math.Abs(XResolution - expectedResolution) > 5)
            errors.Add(
                $"X resolution {XResolution} pix/cm is not close to 500 DPI (~{expectedResolution} pix/cm)"
            );

        if (Math.Abs(YResolution - expectedResolution) > 5)
            errors.Add(
                $"Y resolution {YResolution} pix/cm is not close to 500 DPI (~{expectedResolution} pix/cm)"
            );

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }

    /// <summary>
    /// Gets the capture equipment compliance value (top 4 bits).
    /// </summary>
    public byte GetCaptureEquipmentComplianceValue() =>
        (byte)((CaptureEquipmentCompliance >> 12) & 0x0F);

    /// <summary>
    /// Gets the capture equipment ID value (bottom 12 bits).
    /// </summary>
    public ushort GetCaptureEquipmentId() => (ushort)(CaptureEquipmentCompliance & 0x0FFF);
}
