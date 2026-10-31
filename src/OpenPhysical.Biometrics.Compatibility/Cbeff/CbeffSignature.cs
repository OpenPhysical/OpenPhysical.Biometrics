namespace OpenPhysical.Domains.Cbeff;

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
/// Represents a CMS signature block for CBEFF objects per SP 800-76-2.
/// This is an immutable value object that holds the raw signature bytes.
/// </summary>
/// <param name="SignatureData">The raw CMS SignedData structure bytes</param>
/// <param name="SignatureSize">The size of the signature in bytes (from CBEFF header)</param>
/// <param name="SignatureType">The type of signature (e.g., "CMS", "XMLDSig")</param>
public sealed record CbeffSignature(
    byte[] SignatureData,
    ushort SignatureSize,
    string SignatureType = "CMS"
)
{
    /// <summary>
    /// Creates a CbeffSignature from raw signature bytes with validation.
    /// </summary>
    /// <param name="signatureData">The signature data bytes</param>
    /// <returns>Result containing a new CbeffSignature instance or error</returns>
    public static Result<CbeffSignature> Create(byte[] signatureData) =>
        Result
            .Try(
                () =>
                {
                    if (signatureData.Length == 0)
                        return Result.Failure<CbeffSignature>("Signature data cannot be empty");

                    if (signatureData.Length > ushort.MaxValue)
                        return Result.Failure<CbeffSignature>(
                            $"Signature data too large: {signatureData.Length} bytes"
                        );

                    return Result.Success(
                        new CbeffSignature(
                            SignatureData: (byte[])signatureData.Clone(),
                            SignatureSize: (ushort)signatureData.Length
                        )
                    );
                },
                ex => $"Invalid signature data: {ex.Message}"
            )
            .Bind(result => result);

    /// <summary>
    /// Creates a CbeffSignature from raw signature bytes (unsafe, for compatibility).
    /// </summary>
    /// <param name="signatureData">The signature data bytes</param>
    /// <returns>A new CbeffSignature instance</returns>
    public static CbeffSignature CreateUnsafe(byte[] signatureData)
    {
        ArgumentNullException.ThrowIfNull(signatureData);
        if (signatureData.Length == 0)
        {
            throw new ArgumentException("Signature data cannot be empty", nameof(signatureData));
        }
        if (signatureData.Length > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"Signature data too large: {signatureData.Length} bytes",
                nameof(signatureData)
            );
        }

        return new CbeffSignature(
            SignatureData: (byte[])signatureData.Clone(),
            SignatureSize: (ushort)signatureData.Length
        );
    }

    /// <summary>
    /// Validates the signature size matches the actual data length.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid() => SignatureData.Length == SignatureSize;

    /// <summary>
    /// Gets a copy of the signature data to maintain immutability.
    /// </summary>
    /// <returns>A copy of the signature bytes</returns>
    public byte[] GetSignatureData() => (byte[])SignatureData.Clone();
}
