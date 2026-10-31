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
/// INCITS 378-2004 Extended Data Block per Section 6.6.
/// Base class for optional data that augments standard minutiae data.
/// The extended data area cannot be used alone without the standard portion of the minutiae record.
/// "The optional section of the finger minutiae record is open to placing additional data
/// that may be used by the matching equipment." - Section 6.6
/// </summary>
public abstract record ExtendedDataBlock
{
    /// <summary>
    /// Type Identification Code per INCITS 378-2004 Section 6.6.1.2.
    /// First byte: 0x00 = standard format defined in this standard.
    /// First byte: 0x01-0xFF = vendor-specified format.
    /// Second byte when first is 0x00: 0x01 = ridge count, 0x02 = core/delta, 0x03-0xFF = reserved.
    /// "A value of zero in both bytes is a reserved value and shall not be used."
    /// </summary>
    public ushort TypeIdentificationCode { get; }

    /// <summary>
    /// Length of this extended data segment per INCITS 378-2004 Section 6.6.1.3.
    /// "The length of the extended data section, including the vendor identification
    /// and length of data fields, shall be recorded in two bytes."
    /// This value is used to skip to the next extended data type if the matcher cannot decode this data.
    /// Minimum value is 4 (2 bytes type + 2 bytes length).
    /// </summary>
    public ushort DataLength { get; }

    /// <summary>
    /// Protected constructor for derived extended data types.
    /// </summary>
    /// <param name="typeIdentificationCode">Type ID per Section 6.6.1.2 Table 3.</param>
    /// <param name="dataLength">Total length including type and length fields.</param>
    protected ExtendedDataBlock(ushort typeIdentificationCode, ushort dataLength)
    {
        TypeIdentificationCode = typeIdentificationCode;
        DataLength = dataLength;
    }

    /// <summary>
    /// Validates the extended data block meets INCITS 378-2004 requirements.
    /// Each derived class must implement specific validation per its data format.
    /// </summary>
    /// <returns>Success if valid, Failure with error message if invalid.</returns>
    public abstract Result ValidateCompliance();

    /// <summary>
    /// Validates common extended data block requirements per Section 6.6.1.
    /// </summary>
    protected Result ValidateCommonRequirements()
    {
        var errors = new List<string>();

        // Validate Type ID per Section 6.6.1.2
        if (TypeIdentificationCode == 0x0000)
        {
            errors.Add(
                "Type Identification Code 0x0000 is reserved and shall not be used per INCITS 378-2004 Section 6.6.1.2"
            );
        }

        // Validate minimum data length per Section 6.6.1.3
        if (DataLength < 4)
        {
            errors.Add(
                $"Data length {DataLength} is less than minimum 4 bytes (type + length) per INCITS 378-2004 Section 6.6.1.3"
            );
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }
}
