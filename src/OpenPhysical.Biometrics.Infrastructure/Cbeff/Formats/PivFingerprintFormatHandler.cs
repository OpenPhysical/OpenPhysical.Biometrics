namespace OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;

/// <summary>
/// Format handler for PIV fingerprint templates (INCITS 378) using the new generic architecture.
/// </summary>
[BiometricFormat(0x03, 0x001B, 0x0201, "PIV Fingerprint Minutiae (INCITS 378)")]
public sealed class PivFingerprintFormatHandler : IBiometricFormatHandler<FingerprintRecord>
{
    private readonly ILogger<PivFingerprintFormatHandler> _logger;

    /// <summary>Creates the service with its processing dependencies and operational diagnostics.</summary>
    /// <param name="logger">Operational diagnostics for this service.</param>
    public PivFingerprintFormatHandler(ILogger<PivFingerprintFormatHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string FormatName => "PIV Fingerprint Minutiae (INCITS 378)";

    /// <inheritdoc />
    public byte PatronFormat => 0x03; // PIV patron format

    /// <inheritdoc />
    public ushort BdbFormatOwner => 0x001B; // INCITS

    /// <inheritdoc />
    public ushort BdbFormatType => 0x0201; // Default to no extended data format

    /// <summary>
    /// Determines the CBEFF Format Type based on extended data presence.
    /// Per INCITS 378-2004 Introduction (page vii):
    /// "The sixteen-bit value 0x0201 is used for records with no extended data area
    /// (as determined by the Extended Data Block Length in 6.6.1.1 represented as
    /// the sixteen-bit value 0x0000) and the sixteen-bit value 0x0202 is used for
    /// records that contain an extended data area."
    /// </summary>
    /// <param name="record">The fingerprint record to check.</param>
    /// <returns>0x0201 for no extended data, 0x0202 for records with extended data.</returns>
    public static ushort DetermineCbeffFormatType(FingerprintRecord record)
    {
        // Check if any finger view has extended data
        var hasExtendedData = record.HasExtendedData;

        // Per INCITS 378-2004 page vii
        return hasExtendedData ? (ushort)0x0202 : (ushort)0x0201;
    }

    /// <inheritdoc />
    public Result<FingerprintRecord> ParseBiometricData(ImmutableArray<byte> data)
    {
        return Incits378Parser.ParseIncits378Record(data, _logger);
    }

    /// <inheritdoc />
    public Result<ImmutableArray<byte>> SerializeBiometricData(FingerprintRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        try
        {
            return Result.Success(
                OpenPhysical
                    .Biometrics.Formats.Fingerprints.Incits378Codec.Write(
                        LegacyFingerprintProjection.ToCanonical(record)
                    )
                    .ToImmutableArray()
            );
        }
        catch (Exception error)
            when (error
                    is ArgumentException
                        or InvalidOperationException
                        or ResultFailureException
                        or OverflowException
            )
        {
            return Result.Failure<ImmutableArray<byte>>("INCITS378.CompatibilityProjection");
        }
    }

    /// <inheritdoc />
    public Result ValidateBiometricData(ImmutableArray<byte> data)
    {
        var parsed = OpenPhysical.Biometrics.Formats.Fingerprints.Incits378Codec.Parse(
            data.AsSpan()
        );
        return parsed.IsSuccess
            ? Result.Success()
            : Result.Failure(
                string.Join("; ", parsed.Diagnostics.Select(diagnostic => diagnostic.Code))
            );
    }
}
