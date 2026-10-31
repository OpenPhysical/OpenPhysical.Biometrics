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
/// INCITS 378-2004 Core and Delta Data per Section 6.6.3.
/// Type Identification Code: 0x0002 per Table 3.
/// Contains placement and characteristics of cores and deltas on the fingerprint.
/// "Core and delta points are determined by the overall pattern of ridges in the fingerprint."
/// </summary>
public sealed record CoreDeltaData : ExtendedDataBlock
{
    /// <summary>
    /// Core information type per INCITS 378-2004 Section 6.6.3.1.
    /// Bits 7-6: 00 = no angle, 01 = has angle information.
    /// </summary>
    public CoreInformationType CoreInfoType { get; }

    /// <summary>
    /// Core points per INCITS 378-2004 Section 6.6.3.2-6.6.3.4.
    /// "The number of core points represented shall be recorded in the least significant four bits."
    /// Valid range: 0-15 cores (4 bits).
    /// Each core has position (x,y) and optional angle based on CoreInfoType.
    /// </summary>
    public ImmutableArray<CorePoint> Cores { get; }

    /// <summary>
    /// Delta information type per INCITS 378-2004 Section 6.6.3.5.
    /// Bits 7-6: 00 = has angle, 01 = no angle information.
    /// Note: Opposite convention from core type!
    /// </summary>
    public DeltaInformationType DeltaInfoType { get; }

    /// <summary>
    /// Delta points per INCITS 378-2004 Section 6.6.3.6-6.6.3.8.
    /// "The number of delta points represented shall be recorded in the least significant four bits."
    /// Valid range: 0-15 deltas (4 bits).
    /// Each delta has position (x,y) and optional three angles based on DeltaInfoType.
    /// </summary>
    public ImmutableArray<DeltaPoint> Deltas { get; }

    private CoreDeltaData(
        CoreInformationType coreInfoType,
        ImmutableArray<CorePoint> cores,
        DeltaInformationType deltaInfoType,
        ImmutableArray<DeltaPoint> deltas
    )
        : base(0x0002, CalculateDataLength(coreInfoType, cores, deltaInfoType, deltas))
    {
        CoreInfoType = coreInfoType;
        Cores = cores;
        DeltaInfoType = deltaInfoType;
        Deltas = deltas;
    }

    /// <summary>
    /// Creates core and delta data with validation per INCITS 378-2004 Section 6.6.3.
    /// </summary>
    /// <param name="coreInfoType">Type of core information (with/without angle).</param>
    /// <param name="cores">Collection of core points (0-15).</param>
    /// <param name="deltaInfoType">Type of delta information (with/without angles).</param>
    /// <param name="deltas">Collection of delta points (0-15).</param>
    /// <returns>Success with CoreDeltaData or Failure with error message.</returns>
    public static Result<CoreDeltaData> Create(
        CoreInformationType coreInfoType,
        ImmutableArray<CorePoint> cores,
        DeltaInformationType deltaInfoType,
        ImmutableArray<DeltaPoint> deltas
    )
    {
        var errors = new List<string>();

        // Validate core information type
        if (!Enum.IsDefined(coreInfoType))
        {
            errors.Add($"Invalid core information type: {coreInfoType}");
        }

        // Validate delta information type
        if (!Enum.IsDefined(deltaInfoType))
        {
            errors.Add($"Invalid delta information type: {deltaInfoType}");
        }

        // Validate number of cores per Section 6.6.3.2
        // "Valid values are from 0 to 15"
        if (cores.Length > 15)
        {
            errors.Add(
                $"Too many cores: {cores.Length} (maximum 15 per INCITS 378-2004 Section 6.6.3.2)"
            );
        }

        // Validate number of deltas per Section 6.6.3.6
        // "Valid values are from 0 to 15"
        if (deltas.Length > 15)
        {
            errors.Add(
                $"Too many deltas: {deltas.Length} (maximum 15 per INCITS 378-2004 Section 6.6.3.6)"
            );
        }

        // Validate core angle consistency
        if (coreInfoType == CoreInformationType.NoAngle)
        {
            for (int i = 0; i < cores.Length; i++)
            {
                if (cores[i].Angle.HasValue)
                {
                    errors.Add($"Core at index {i} has angle but CoreInfoType indicates no angle");
                }
            }
        }
        else if (coreInfoType == CoreInformationType.HasAngle)
        {
            for (int i = 0; i < cores.Length; i++)
            {
                if (!cores[i].Angle.HasValue)
                {
                    errors.Add($"Core at index {i} missing angle");
                }
            }
        }

        // Validate delta angle consistency
        if (deltaInfoType == DeltaInformationType.NoAngles)
        {
            for (int i = 0; i < deltas.Length; i++)
            {
                if (deltas[i].Angles.HasValue)
                {
                    errors.Add(
                        $"Delta at index {i} has angles but DeltaInfoType indicates no angles"
                    );
                }
            }
        }
        else if (deltaInfoType == DeltaInformationType.HasAngles)
        {
            for (int i = 0; i < deltas.Length; i++)
            {
                if (!deltas[i].Angles.HasValue)
                {
                    errors.Add($"Delta at index {i} missing angles");
                }
            }
        }

        // Per Section 6.6.3: "There may be one or more core points and zero or more delta points"
        // Note: We allow zero cores despite the text saying "one or more" because:
        // 1. The valid range explicitly includes 0 in Section 6.6.3.2
        // 2. Some fingerprint patterns may not have detectable cores

        return errors.Count == 0
            ? Result.Success(new CoreDeltaData(coreInfoType, cores, deltaInfoType, deltas))
            : Result.Failure<CoreDeltaData>(string.Join("; ", errors));
    }

    /// <summary>
    /// Validates the core and delta data meets INCITS 378-2004 compliance requirements.
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

        // Verify Type ID is correct for core/delta data
        if (TypeIdentificationCode != 0x0002)
        {
            errors.Add(
                $"Core/delta data must have Type ID 0x0002, got 0x{TypeIdentificationCode:X4}"
            );
        }

        // Verify data length calculation
        var expectedLength = CalculateDataLength(CoreInfoType, Cores, DeltaInfoType, Deltas);
        if (DataLength != expectedLength)
        {
            errors.Add(
                $"Data length mismatch: header says {DataLength}, calculated {expectedLength}"
            );
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(string.Join("; ", errors));
    }

    /// <summary>
    /// Calculates data length per INCITS 378-2004 Section 6.6.3.9.
    /// See diagram on page 16 for complete format.
    /// </summary>
    private static ushort CalculateDataLength(
        CoreInformationType coreInfoType,
        ImmutableArray<CorePoint> cores,
        DeltaInformationType deltaInfoType,
        ImmutableArray<DeltaPoint> deltas
    )
    {
        // Start with fixed overhead
        // 2 bytes for type ID (Section 6.6.1.2)
        // 2 bytes for length field (Section 6.6.1.3)
        // 1 byte for core info type + count (Section 6.6.3.1-6.6.3.2)
        // 1 byte for delta info type + count (Section 6.6.3.5-6.6.3.6)
        ushort length = 4 + 1 + 1;

        // Add core data
        foreach (var core in cores)
        {
            // 4 bytes for x,y position (2 bytes each per Section 6.6.3.3)
            length += 4;

            // 1 byte for angle if present (Section 6.6.3.4)
            if (coreInfoType == CoreInformationType.HasAngle)
            {
                length += 1;
            }
        }

        // Add delta data
        foreach (var delta in deltas)
        {
            // 4 bytes for x,y position (2 bytes each per Section 6.6.3.7)
            length += 4;

            // 3 bytes for three angles if present (Section 6.6.3.8)
            if (deltaInfoType == DeltaInformationType.HasAngles)
            {
                length += 3;
            }
        }

        return length;
    }
}
