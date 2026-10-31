namespace OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Core.Domain;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;

/// <summary>
/// Format handler for PIV facial images (INCITS 385) using the new generic architecture.
/// </summary>
[BiometricFormat(0x03, 0x001B, 0x0501, "PIV Facial Image (INCITS 385)")]
public sealed class PivFacialFormatHandler : BiometricFormatHandlerBase<FacialImageRecord>
{
    /// <inheritdoc />
    public override string FormatName => "PIV Facial Image (INCITS 385)";

    /// <inheritdoc />
    public override byte PatronFormat => 0x03; // PIV patron format

    /// <inheritdoc />
    public override ushort BdbFormatOwner => 0x001B; // INCITS

    /// <inheritdoc />
    public override ushort BdbFormatType => 0x0501; // Facial image

    /// <inheritdoc />
    public override Result<FacialImageRecord> ParseBiometricData(ImmutableArray<byte> data)
    {
        return Incits385Parser.ParseIncits385Record(data);
    }

    /// <inheritdoc />
    public override Result<ImmutableArray<byte>> SerializeBiometricData(FacialImageRecord record)
    {
        try
        {
            return Result.Success(
                OpenPhysical
                    .Biometrics.Formats.Faces.Incits385Codec.Write(
                        LegacyFacialProjection.ToCanonical(record)
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
            return Result.Failure<ImmutableArray<byte>>("INCITS385.CompatibilityProjection");
        }
    }

    /// <inheritdoc />
    public override Result ValidateBiometricData(ImmutableArray<byte> data)
    {
        var parsed = ParseBiometricData(data);
        if (parsed.IsFailure)
            return Result.Failure(parsed.Error);
        var image = parsed.Value.ValidateImageFormat();
        return image.IsSuccess ? Result.Success() : Result.Failure(image.Error);
    }
}
