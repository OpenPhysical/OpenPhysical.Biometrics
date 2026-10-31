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
/// INCITS 378-2004 Vendor-Specific Extended Data per Section 6.6.
/// Vendor-specific extended data uses Type Identification Codes >= 0x0003.
/// </summary>
public sealed record VendorData : ExtendedDataBlock
{
    /// <summary>
    /// Vendor identification code per Section 6.6.
    /// This identifies the organization that defined the proprietary data format.
    /// </summary>
    public ushort VendorCode { get; }

    /// <summary>
    /// Vendor-specific data payload.
    /// Format and content are defined by the vendor identified by VendorCode.
    /// </summary>
    public ImmutableArray<byte> Data { get; }

    private VendorData(
        ushort typeIdentificationCode,
        ushort dataLength,
        ushort vendorCode,
        ImmutableArray<byte> data
    )
        : base(typeIdentificationCode, dataLength)
    {
        VendorCode = vendorCode;
        Data = data;
    }

    /// <summary>
    /// Creates vendor-specific extended data with validation.
    /// Type Identification Code is assigned automatically starting from 0x0003.
    /// </summary>
    /// <param name="vendorCode">Vendor identification code.</param>
    /// <param name="data">Vendor-specific data payload.</param>
    /// <returns>Success with VendorData or Failure with error message.</returns>
    public static Result<VendorData> Create(ushort vendorCode, ImmutableArray<byte> data)
    {
        // Assign next available vendor type code (0x0003 and above)
        // In a real implementation, this might be managed by a registry
        ushort typeCode = GetNextVendorTypeCode();

        // Calculate data length: type (2) + length (2) + vendor code (2) + data
        var calculatedLength = 4 + 2 + data.Length;

        // Validate total length doesn't exceed maximum
        if (calculatedLength > ushort.MaxValue)
        {
            return Result.Failure<VendorData>(
                $"Vendor data length {calculatedLength} exceeds maximum 65535 bytes"
            );
        }

        return Result.Success(
            new VendorData(
                typeCode,
                (ushort)calculatedLength,
                vendorCode,
                data.IsDefault ? ImmutableArray<byte>.Empty : data
            )
        );
    }

    /// <summary>
    /// Creates vendor data for parsing with specific type code.
    /// Used during parsing when the type code is read from the data stream.
    /// </summary>
    /// <param name="typeIdentificationCode">Type ID from data stream.</param>
    /// <param name="vendorCode">Vendor identification code.</param>
    /// <param name="data">Vendor-specific data payload.</param>
    /// <returns>A vendor data instance without validation.</returns>
    public static VendorData CreateForParsing(
        ushort typeIdentificationCode,
        ushort vendorCode,
        ImmutableArray<byte> data
    )
    {
        var calculatedLength = 4 + 2 + data.Length;
        return new VendorData(typeIdentificationCode, (ushort)calculatedLength, vendorCode, data);
    }

    /// <summary>
    /// Validates vendor data compliance.
    /// Vendor data format is defined by the vendor, so validation is minimal.
    /// </summary>
    /// <returns>Always succeeds for vendor data.</returns>
    public override Result ValidateCompliance() =>
        // Vendor data format is proprietary, so we can't validate content
        // Only basic structural validation is possible
        Result.Success();

    /// <summary>
    /// Gets next available vendor type code.
    /// Simple implementation - starts at 0x0003 since 0x0001 and 0x0002 are reserved.
    /// </summary>
    private static ushort GetNextVendorTypeCode() =>
        // In a real implementation, this would track used codes
        // For simplicity, we start at 0x0003
        0x0003;
}
