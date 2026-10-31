namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using System;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Cbeff;

/// <summary>
/// Service implementation for encoding biometric data into CBEFF format.
/// </summary>
public class CbeffEncoderService : ICbeffEncoder
{
    /// <summary>
    /// Encodes biometric data into a CBEFF payload.
    /// </summary>
    public async Task<Result<CbeffPayload>> EncodeAsync(
        byte[] biometricData,
        BiometricModality modality,
        BiometricFormat format,
        CbeffMetadata metadata
    )
    {
        if (biometricData == null)
            return Result.Failure<CbeffPayload>("Biometric data cannot be null");
        if (biometricData.Length == 0)
            return Result.Failure<CbeffPayload>("Biometric data cannot be empty");

        try
        {
            // Create CBEFF header
            var headerResult = CreateHeader(
                (uint)biometricData.Length,
                0, // No signature initially
                modality,
                format,
                metadata
            );

            if (headerResult.IsFailure)
                return Result.Failure<CbeffPayload>(headerResult.Error);

            // Create payload
            var payloadResult = CbeffPayload.Create(
                headerResult.Value,
                biometricData,
                Maybe<CbeffSignature>.None
            );

            return payloadResult;
        }
        catch (Exception ex)
        {
            return Result.Failure<CbeffPayload>($"Error encoding CBEFF data: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a CBEFF header for the given parameters.
    /// </summary>
    public Result<CbeffHeader> CreateHeader(
        uint biometricDataLength,
        ushort signatureLength,
        BiometricModality modality,
        BiometricFormat format,
        CbeffMetadata metadata
    )
    {
        try
        {
            // Prepare creator name (18 bytes, null-padded)
            var creatorBytes = new byte[18];
            var creatorNameBytes = Encoding.ASCII.GetBytes(metadata.CreatorName);
            Array.Copy(creatorNameBytes, 0, creatorBytes, 0, Math.Min(creatorNameBytes.Length, 18));

            // Prepare reserved bytes (4 bytes, all zero)
            var reservedBytes = new byte[4];

            // Biometric type (modality as 4 bytes with only last byte used)
            var modalityValue = GetModalityValue(modality);
            var biometricType = (uint)modalityValue; // PIV uses only the last byte

            // Create the header using the factory method
            return CbeffHeader.Create(
                metadata.PatronHeaderVersion,
                metadata.SecurityOptions,
                biometricDataLength,
                signatureLength,
                format.FormatOwner,
                format.FormatType,
                metadata.CreationDate,
                metadata.ValidityStartDate,
                metadata.ValidityEndDate,
                biometricType,
                0x00, // Biometric subtype (PIV doesn't typically use this)
                metadata.BiometricQuality,
                creatorBytes,
                metadata.FascN,
                reservedBytes
            );
        }
        catch (Exception ex)
        {
            return Result.Failure<CbeffHeader>($"Error creating CBEFF header: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the byte value for a biometric modality.
    /// </summary>
    private static byte GetModalityValue(BiometricModality modality)
    {
        return modality.Value switch
        {
            "FACE" => 0x02,
            "FINGERPRINT" => 0x08,
            "IRIS" => 0x10,
            "VOICE" => 0x20,
            "SIGNATURE" => 0x40,
            _ => 0x00,
        };
    }
}
