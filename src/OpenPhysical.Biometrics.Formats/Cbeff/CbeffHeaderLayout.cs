// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Cbeff;

using System.Buffers.Binary;
using CSharpFunctionalExtensions;

// Byte-level encoding and checking of the fixed 88-byte patron header.
internal static class CbeffHeaderLayout
{
    internal static UnitResult<FormatError> CheckFields(
        int creatorLength,
        int fascNLength,
        uint biometricType
    )
    {
        if (creatorLength != 18)
            return Invalid("CbeffCreator", "The creator field contains exactly 18 bytes.");
        if (fascNLength != 25)
            return Invalid("CbeffFascN", "The FASC-N field contains exactly 25 bytes.");
        return biometricType > 0xFFFFFF
            ? Invalid("CbeffBiometricType", "The biometric type is a 24-bit mask.")
            : UnitResult.Success<FormatError>();
    }

    internal static byte[] Build(
        CbeffHeaderFields fields,
        ReadOnlySpan<byte> creator,
        ReadOnlySpan<byte> fascN
    )
    {
        var result = new byte[CbeffHeader.Size];
        result[0] = fields.PatronHeaderVersion;
        result[1] = fields.SecurityOptions;
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(2), fields.BiometricDataLength);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(6), fields.SignatureLength);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(8), fields.FormatOwner);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(10), fields.FormatType);
        CbeffTimestampCodec.Encode(fields.CreationDate).CopyTo(result, 12);
        CbeffTimestampCodec.Encode(fields.ValidityNotBefore).CopyTo(result, 20);
        CbeffTimestampCodec.Encode(fields.ValidityNotAfter).CopyTo(result, 28);
        result[36] = (byte)(fields.BiometricType >> 16);
        result[37] = (byte)(fields.BiometricType >> 8);
        result[38] = (byte)fields.BiometricType;
        result[39] = fields.BiometricDataType;
        result[40] = unchecked((byte)fields.Quality);
        creator.CopyTo(result.AsSpan(41));
        fascN.CopyTo(result.AsSpan(59));
        return result;
    }

    internal static Result<
        (DateTimeOffset Creation, DateTimeOffset NotBefore, DateTimeOffset NotAfter),
        FormatError
    > DecodeDates(ReadOnlySpan<byte> header)
    {
        var creation = CbeffTimestampCodec.Decode(header.Slice(12, 8));
        var notBefore = CbeffTimestampCodec.Decode(header.Slice(20, 8));
        var notAfter = CbeffTimestampCodec.Decode(header.Slice(28, 8));
        return creation.Bind(c => notBefore.Bind(b => notAfter.Map(a => (c, b, a))));
    }

    private static UnitResult<FormatError> Invalid(string code, string message) =>
        UnitResult.Failure(FormatError.Invalid(code, message));
}
