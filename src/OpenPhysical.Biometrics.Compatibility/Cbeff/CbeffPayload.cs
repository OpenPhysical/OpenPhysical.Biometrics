namespace OpenPhysical.Domains.Cbeff;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Biometrics;
using CSharpFunctionalExtensions;

/// <summary>
/// Represents a complete CBEFF (Common Biometric Exchange Formats Framework) payload
/// including header, biometric data block, and optional signature.
/// This is an immutable value object per SP 800-76-2.
/// </summary>
public sealed record CbeffPayload
{
    /// <summary>
    /// Gets the CBEFF header containing metadata about the biometric data.
    /// </summary>
    public CbeffHeader Header { get; }

    /// <summary>
    /// Gets the biometric data block (BDB) containing the actual biometric data.
    /// For facial images, this is INCITS 385 format data.
    /// For fingerprints, this is INCITS 378 minutiae data.
    /// </summary>
    public byte[] BiometricDataBlock { get; }

    /// <summary>
    /// Gets the optional signature block for signed CBEFF objects.
    /// </summary>
    public Maybe<CbeffSignature> Signature { get; }

    /// <summary>
    /// Gets the biometric modality from the header.
    /// </summary>
    public BiometricModality Modality => Header.GetBiometricModality();

    /// <summary>
    /// Gets the biometric format from the header.
    /// </summary>
    public BiometricFormat Format => Header.GetBiometricFormat();

    /// <summary>
    /// Constructor for the CBEFF payload.
    /// </summary>
    public CbeffPayload(
        CbeffHeader header,
        byte[] biometricDataBlock,
        Maybe<CbeffSignature> signature = default
    )
    {
        Header = header;
        BiometricDataBlock = biometricDataBlock;
        Signature = signature;
    }

    /// <summary>
    /// Creates a new CBEFF payload with validation.
    /// </summary>
    /// <param name="header">The CBEFF header</param>
    /// <param name="biometricDataBlock">The biometric data</param>
    /// <param name="signature">Optional signature</param>
    /// <returns>A new CbeffPayload instance</returns>
    public static Result<CbeffPayload> Create(
        CbeffHeader header,
        byte[] biometricDataBlock,
        Maybe<CbeffSignature> signature = default
    ) =>
        Result
            .Try(
                () =>
                {
                    if (biometricDataBlock.Length == 0)
                        return Result.Failure<CbeffPayload>("Biometric data block cannot be empty");

                    if (biometricDataBlock.Length != header.BdbLength)
                        return Result.Failure<CbeffPayload>(
                            $"Biometric data block length ({biometricDataBlock.Length}) does not match header BDB length ({header.BdbLength})"
                        );

                    if (signature.HasValue && signature.Value.SignatureSize != header.SbLength)
                        return Result.Failure<CbeffPayload>(
                            $"Signature size ({signature.Value.SignatureSize}) does not match header SB length ({header.SbLength})"
                        );

                    return Result.Success(
                        new CbeffPayload(
                            header: header,
                            biometricDataBlock: (byte[])biometricDataBlock.Clone(),
                            signature: signature
                        )
                    );
                },
                ex => $"Invalid payload data: {ex.Message}"
            )
            .Bind(result => result);

    /// <summary>
    /// Creates a CBEFF payload for an unsigned biometric object.
    /// </summary>
    /// <param name="header">The CBEFF header</param>
    /// <param name="biometricDataBlock">The biometric data</param>
    /// <returns>A new unsigned CbeffPayload instance</returns>
    public static Result<CbeffPayload> CreateUnsigned(
        CbeffHeader header,
        byte[] biometricDataBlock
    ) =>
        // Note: We can't use 'with' on the existing header since it doesn't have a SbLength property
        // This would need to be adjusted based on the actual CbeffHeader implementation
        Create(header, biometricDataBlock, Maybe<CbeffSignature>.None);

    /// <summary>
    /// Adds or updates the signature on this CBEFF payload.
    /// </summary>
    /// <param name="signature">The signature to add</param>
    /// <returns>A new CbeffPayload with the signature</returns>
    public CbeffPayload WithSignature(CbeffSignature signature) =>
        new(Header, BiometricDataBlock, Maybe<CbeffSignature>.From(signature));

    /// <summary>
    /// Validates the internal consistency of the CBEFF payload.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        // Check header/data consistency
        if (BiometricDataBlock.Length != Header.BdbLength)
        {
            return false;
        }

        // Check signature consistency if present
        if (Signature.HasValue)
        {
            if (Signature.Value.SignatureSize != Header.SbLength)
            {
                return false;
            }
            if (!Signature.Value.IsValid())
            {
                return false;
            }
        }
        else if (Header.SbLength != 0)
        {
            // Header indicates signature but none present
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets a copy of the biometric data block to maintain immutability.
    /// </summary>
    /// <returns>A copy of the biometric data bytes</returns>
    public byte[] GetBiometricDataBlock() => (byte[])BiometricDataBlock.Clone();
}
